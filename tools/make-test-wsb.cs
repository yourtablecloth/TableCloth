#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

// ── 테스트용 Spork 단독 실행 wsb 생성기 (File-based App) ─────────────────────────
// 목적: Spork 를 publish(self-contained 패키징) 하지 않고, 일반 `dotnet build` 출력물을
//       호스트의 dotnet 설치 폴더와 함께 Windows Sandbox 에 마운트해 단독 실행을 빠르게
//       테스트한다. 식탁보 호스트 앱의 framework-dependent dev 흐름과 같은 원리:
//       마운트된 dotnet 을 DOTNET_ROOT 로 주입해 런타임을 공급한다.
//
// 마운트는 딱 두 개 — (1) Spork 빌드 출력 폴더(RW), (2) 호스트 dotnet 루트(RO).
//
// 사용:
//   dotnet run tools/make-test-wsb.cs                      # Spork 빌드 + wsb 생성
//   dotnet run tools/make-test-wsb.cs -- --no-build        # 빌드 생략(기존 출력 사용)
//   dotnet run tools/make-test-wsb.cs -- --config Release
//   dotnet run tools/make-test-wsb.cs -- --launch          # 생성 후 Windows Sandbox 실행
//   dotnet run tools/make-test-wsb.cs -- --out D:\tmp\spork-test.wsb
// ───────────────────────────────────────────────────────────────────────────────

const string SandboxDesktop = @"C:\Users\WDAGUtilityAccount\Desktop";

if (args.Contains("--help") || args.Contains("-h") || args.Contains("-?"))
{
    PrintHelp();
    return 0;
}

var config = GetArgValue("--config") ?? "Debug";
var noBuild = args.Contains("--no-build");
var noImages = args.Contains("--no-images");
var launch = args.Contains("--launch");
var outArg = GetArgValue("--out");
var sporkOutputArg = GetArgValue("--spork-output");
var dotnetRootArg = GetArgValue("--dotnet-root");

var repoRoot = FindRepoRoot();
Directory.SetCurrentDirectory(repoRoot);
Console.WriteLine($"Repo root:    {repoRoot}");

var sporkProject = Path.Combine("src", "Spork", "Spork.csproj");

if (!noBuild)
{
    Console.WriteLine($"Building Spork ({config})...");
    var exit = await RunAsync("dotnet", $"build \"{sporkProject}\" -c {config} --nologo -v m");
    if (exit != 0)
    {
        Console.Error.WriteLine("Spork build failed.");
        return 1;
    }
}

// Spork.exe 위치 해석 (일반 build 출력: src/Spork/bin/<config>/<tfm>/Spork.exe)
var sporkOutputDir = sporkOutputArg ?? ResolveSporkOutput(config);
if (sporkOutputDir is null || !File.Exists(Path.Combine(sporkOutputDir, "Spork.exe")))
{
    Console.Error.WriteLine($"Spork.exe not found. Build first (omit --no-build) or pass --spork-output. (looked under src/Spork/bin/{config})");
    return 1;
}
Console.WriteLine($"Spork output: {sporkOutputDir}");

// 카탈로그 아이콘을 출력 폴더의 images\ 로 미리 풀어 둔다(오프라인 동봉). 단독 Spork 의
// ServiceLogoConverter 는 AppContext.BaseDirectory\images\{id}.png 를 1순위로 읽으므로,
// 마운트된 이 폴더에 아이콘이 있으면 원격 다운로드 없이 즉시 로컬에서 그려진다(첫 로딩 빈 화면→일괄 등장 방지).
if (!noImages)
    BundleCatalogImages(Path.Combine(sporkOutputDir, "images"));

// 호스트 dotnet 설치 루트 해석 (식탁보 SandboxBuilder.TryResolveHostDotnetRoot 와 동일 우선순위)
var dotnetRoot = dotnetRootArg ?? ResolveDotnetRoot();
if (dotnetRoot is null)
{
    Console.Error.WriteLine("Could not resolve host dotnet root. Pass --dotnet-root <dir>.");
    return 1;
}
Console.WriteLine($"dotnet root:  {dotnetRoot}");

var sporkLeaf = Path.GetFileName(sporkOutputDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
var dotnetLeaf = Path.GetFileName(dotnetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

// SandboxFolder 미지정 매핑은 모두 Desktop\<leaf> 로 노출되므로 leaf 충돌이 있으면 안 된다.
if (string.Equals(sporkLeaf, dotnetLeaf, StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine($"Mount leaf name collision: both map to Desktop\\{sporkLeaf}. Pass --spork-output/--dotnet-root with distinct leaf names.");
    return 1;
}

// 샌드박스 안에서 실행할 런처 cmd 를 Spork 출력 폴더에 떨군다(추가 마운트 회피).
//  - %~dp0      = cmd 자신의 폴더(= Desktop\<sporkLeaf>) → Spork.exe 를 상대 참조.
//  - DOTNET_ROOT = 마운트된 dotnet 폴더(Desktop\<dotnetLeaf>) → framework-dependent 런타임 공급.
//  - CI 정책/Edge HW 가속 끄기는 식탁보 StartupScript 와 동일한 안전장치(미서명 exe SAC 강제종료 회피).
const string RunCmdName = "_spork-test-run.cmd";
var runCmdLines = new[]
{
    "@echo off",
    "pushd \"%~dp0\"",
    $"set DOTNET_ROOT={SandboxDesktop}\\{dotnetLeaf}",
    "set PATH=%DOTNET_ROOT%;%PATH%",
    "reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\CI\\Policy\" /v VerifiedAndReputablePolicyState /t REG_DWORD /d 0 /f >nul 2>&1",
    "reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge\" /v HardwareAccelerationModeEnabled /t REG_DWORD /d 0 /f >nul 2>&1",
    "\"%SystemRoot%\\System32\\citool.exe\" --refresh >nul 2>&1",
    "start \"Spork\" \"%~dp0Spork.exe\"",
    "popd",
};
File.WriteAllText(Path.Combine(sporkOutputDir, RunCmdName),
    string.Join("\r\n", runCmdLines) + "\r\n", new UTF8Encoding(false));

// wsb 생성. SandboxFolder 는 의도적으로 비운다(프로젝트 관례: 모든 매핑은 Desktop\<leaf> 로 노출).
var logonCommand = $@"{SandboxDesktop}\{sporkLeaf}\{RunCmdName}";
var configElement = new XElement("Configuration",
    new XElement("Networking", "Enable"),
    new XElement("vGPU", "Disable"),
    new XElement("MappedFolders",
        new XElement("MappedFolder",
            new XElement("HostFolder", Path.GetFullPath(sporkOutputDir)),
            new XElement("ReadOnly", "false")),
        new XElement("MappedFolder",
            new XElement("HostFolder", Path.GetFullPath(dotnetRoot)),
            new XElement("ReadOnly", "true"))),
    new XElement("LogonCommand",
        new XElement("Command", logonCommand)));

var outPath = Path.GetFullPath(outArg ?? Path.Combine(Path.GetTempPath(), "spork-test.wsb"));
new XDocument(configElement).Save(outPath);

Console.WriteLine();
Console.WriteLine($"Generated:    {outPath}");
Console.WriteLine($"  Spork.exe   -> {SandboxDesktop}\\{sporkLeaf}\\Spork.exe");
Console.WriteLine($"  DOTNET_ROOT -> {SandboxDesktop}\\{dotnetLeaf}");
Console.WriteLine();
Console.WriteLine($"Launch with:  explorer \"{outPath}\"   (or)   WindowsSandbox.exe \"{outPath}\"");

if (launch)
{
    var wsbExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsSandbox.exe");
    Console.WriteLine();
    if (File.Exists(wsbExe))
    {
        Console.WriteLine("Launching Windows Sandbox...");
        Process.Start(new ProcessStartInfo(wsbExe, $"\"{outPath}\"") { UseShellExecute = true });
    }
    else
    {
        Console.Error.WriteLine($"WindowsSandbox.exe not found at {wsbExe}. Is the Windows Sandbox feature enabled? Opening via shell instead.");
        Process.Start(new ProcessStartInfo(outPath) { UseShellExecute = true });
    }
}

return 0;

// ── helpers ──────────────────────────────────────────────────────────────────
string? GetArgValue(string name)
{
    var idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

string FindRepoRoot()
{
    var dir = GetScriptDirectory();
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir, "TableCloth.slnx")))
            return dir;
        dir = Path.GetDirectoryName(dir);
    }
    // 폴백: 스크립트 폴더의 부모(tools/ 의 부모 = repo root)
    return Path.GetDirectoryName(GetScriptDirectory()) ?? Directory.GetCurrentDirectory();
}

string GetScriptDirectory()
{
    var scriptPath = Environment.GetCommandLineArgs()
        .FirstOrDefault(a => a.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
    if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
        return Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? Directory.GetCurrentDirectory();
    return Directory.GetCurrentDirectory();
}

string? ResolveSporkOutput(string cfg)
{
    var binDir = Path.Combine("src", "Spork", "bin", cfg);
    if (!Directory.Exists(binDir))
        return null;
    var exe = Directory.GetFiles(binDir, "Spork.exe", SearchOption.AllDirectories).FirstOrDefault();
    return exe is null ? null : Path.GetDirectoryName(exe);
}

string? ResolveDotnetRoot()
{
    var fromEnv = Environment.GetEnvironmentVariable("DOTNET_ROOT");
    if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
        return fromEnv;

    var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    if (!string.IsNullOrEmpty(pf))
    {
        var sys = Path.Combine(pf, "dotnet");
        if (Directory.Exists(sys)) return sys;
    }

    var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    if (!string.IsNullOrEmpty(lad))
    {
        var user = Path.Combine(lad, "Microsoft", "dotnet");
        if (Directory.Exists(user)) return user;
    }

    return null;
}

async Task<int> RunAsync(string cmd, string cmdArgs)
{
    var psi = new ProcessStartInfo { FileName = cmd, Arguments = cmdArgs, UseShellExecute = false };
    using var p = Process.Start(psi)!;
    await p.WaitForExitAsync();
    return p.ExitCode;
}

// 카탈로그 아이콘(Images.zip)을 대상 images\ 폴더로 풀어 둔다. 로컬 repo 의 src/TableCloth/Images.zip
// 을 우선 쓰고, 없으면 카탈로그 서버에서 받는다. 변환기가 쓰는 .png 만 추출(.ico 는 제외해 용량 절감).
void BundleCatalogImages(string imagesDir)
{
    try
    {
        if (Directory.Exists(imagesDir) && Directory.EnumerateFiles(imagesDir, "*.png").Any())
        {
            Console.WriteLine($"Icons:        already present in {imagesDir}");
            return;
        }

        var zipPath = ResolveImagesZip();
        if (zipPath is null)
        {
            Console.WriteLine("Icons:        Images.zip unavailable — catalog will load icons remotely.");
            return;
        }

        Directory.CreateDirectory(imagesDir);
        using var archive = ZipFile.OpenRead(zipPath);
        var count = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue; // 디렉터리 항목
            if (!entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            entry.ExtractToFile(Path.Combine(imagesDir, entry.Name), overwrite: true);
            count++;
        }
        Console.WriteLine($"Icons:        bundled {count} png into {imagesDir}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Icons:        bundling skipped ({ex.Message}) — catalog will load icons remotely.");
    }
}

string? ResolveImagesZip()
{
    var local = Path.Combine("src", "TableCloth", "Images.zip");
    if (File.Exists(local))
        return local;

    try
    {
        var tmp = Path.Combine(Path.GetTempPath(), "TableCloth_Images.zip");
        using var http = new System.Net.Http.HttpClient();
        var bytes = http.GetByteArrayAsync("https://yourtablecloth.app/TableClothCatalog/Images.zip").GetAwaiter().GetResult();
        File.WriteAllBytes(tmp, bytes);
        return tmp;
    }
    catch
    {
        return null;
    }
}

void PrintHelp()
{
    Console.WriteLine("""
        Spork 단독 테스트 wsb 생성기 (.NET 10 File-based App)

        Usage: dotnet run tools/make-test-wsb.cs [-- <options>]

        Options:
          --config <Debug|Release>   빌드/출력 구성 (기본: Debug)
          --no-build                 Spork 빌드 생략(기존 출력 사용)
          --no-images                카탈로그 아이콘(Images.zip) 동봉 생략(원격 로드)
          --spork-output <dir>       Spork.exe 가 있는 출력 폴더 직접 지정
          --dotnet-root <dir>        호스트 dotnet 설치 루트 직접 지정
          --out <path>               생성할 .wsb 경로 (기본: %TEMP%\spork-test.wsb)
          --launch                   생성 후 Windows Sandbox 실행
          -h, --help                 도움말
        """);
}

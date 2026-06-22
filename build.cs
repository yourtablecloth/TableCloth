#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

// Configuration
var appId = "TableCloth";
var solutionFile = "TableCloth.slnx";
var mainProject = Path.Combine("src", "TableCloth", "TableCloth.csproj");
// Resources 폴더는 Phase 1.2에서 TableCloth.App으로 이전됨.
var iconPath = Path.Combine("src", "TableCloth.App", "Resources", "SandboxIcon.ico");
var directoryBuildProps = "Directory.Build.Props";
// 프로젝트 수준은 AnyCPU이고 publish 시점에 RID로 결정. 여기서는 RID 접미사로 사용.
var platforms = new[] { "x64", "arm64" };

// Parse command line arguments
var includeDebug = args.Contains("--debug") || args.Contains("-d");
var skipBuild = args.Contains("--skip-build") || args.Contains("-s");
var showHelp = args.Contains("--help") || args.Contains("-h") || args.Contains("-?");
// 코드 서명: --sign 으로 활성화. 인증서 주체는 --sign-subject 또는 환경 변수
// TABLECLOTH_SIGN_SUBJECT (sign-release.ps1 과 동일). SimplySign 세션이 떠 있어야 함.
var doSign = args.Contains("--sign") || args.Contains("-S");
var signSubject = GetArgValue("--sign-subject") ?? Environment.GetEnvironmentVariable("TABLECLOTH_SIGN_SUBJECT");
var timestampUrl = GetArgValue("--timestamp-url") ?? "http://time.certum.pl";

if (showHelp)
{
    Console.WriteLine("""
        TableCloth Build Script (.NET 10)
        
        Usage: dotnet run --file build.cs [options]
        
        Options:
          -d, --debug              Include Debug configuration
          -s, --skip-build         Skip build step (use existing publish output)
          -S, --sign               Code-sign Release packages via Velopack (vpk --signParams).
                                   Signs app binaries + Update.exe + Setup.exe during pack.
                                   Requires an active SimplySign Desktop session.
              --sign-subject <CN>  Certificate subject substring (or set TABLECLOTH_SIGN_SUBJECT).
              --timestamp-url <u>  RFC 3161 timestamp URL (default: http://time.certum.pl).
          -h, --help               Show this help message
        """);
    return 0;
}

var configurations = includeDebug ? ["Debug", "Release"] : new[] { "Release" };

// 서명 활성화 시 사전 검증: 주체 + CurrentUser\My 에 개인 키를 가진 인증서 존재.
// (긴 빌드 후 pack 단계에서 실패하는 것을 막기 위해 빌드 전에 확인)
if (doSign)
{
    if (string.IsNullOrWhiteSpace(signSubject))
    {
        Console.Error.WriteLine("Error: --sign requires a certificate subject. Pass --sign-subject \"<CN>\" or set TABLECLOTH_SIGN_SUBJECT.");
        return 1;
    }
    if (!HasSigningCert(signSubject))
    {
        Console.Error.WriteLine($"Error: no code-signing certificate matching '{signSubject}' (with private key) in CurrentUser\\My. Is SimplySign Desktop logged in?");
        return 1;
    }
    Console.WriteLine($"Code signing ENABLED — subject: {signSubject}, timestamp: {timestampUrl}");
}

await RunBuildAsync(configurations, platforms, skipBuild);
return 0;

async Task RunBuildAsync(string[] configs, string[] plats, bool skip)
{
    var scriptDir = GetScriptDirectory();
    Directory.SetCurrentDirectory(scriptDir);

    Console.WriteLine("=======================");
    Console.WriteLine("TableCloth Build Script");
    Console.WriteLine("=======================");
    Console.WriteLine();

    // Install vpk CLI
    _ = await RunCommandAsync("dotnet", "tool install -g vpk");

    // Get Git commit hash
    var gitCommit = await RunCommandAsync("git", "rev-parse --short HEAD");
    Console.WriteLine($"Git Commit: {gitCommit}");

    // Extract version from Directory.Build.props
    var projectVersion = GetProjectVersion(directoryBuildProps);
    Console.WriteLine($"Project Version: {projectVersion}");

    // Convert to SemVer (Major.Minor.Patch)
    var versionParts = projectVersion.Split('.');
    var version = $"{versionParts[0]}.{versionParts[1]}.{versionParts[2]}";
    Console.WriteLine($"Build Version: {version}");
    Console.WriteLine();

    if (!skip)
    {
        // Restore packages
        Console.WriteLine("Restoring packages...");
        await RunCommandAsync("dotnet", $"restore {solutionFile}");
        Console.WriteLine();
    }

    foreach (var platform in plats)
    {
        foreach (var config in configs)
        {
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine($"Building {config} - {platform} [v{version}+{gitCommit}]");
            Console.WriteLine("========================================");

            var publishDir = Path.Combine("publish", config, $"win-{platform}");
            var releasesDir = Path.Combine("Releases", config, platform);

            if (!skip)
            {
                // Build solution (AnyCPU; csproj는 RID/SelfContained를 publish 시점에만 활성화)
                Console.WriteLine();
                Console.WriteLine("Building solution...");
                await RunCommandAsync("dotnet",
                    $"build {solutionFile} -c {config}");

                // Publish TableCloth — TableCloth.csproj의 조건부 PropertyGroup이 RID 명시 시
                // PublishSingleFile=true / PublishReadyToRun=true / EnableCompressionInSingleFile=true 등을
                // 자동 활성화한다. 여기서는 RID와 SelfContained만 명시.
                Console.WriteLine();
                Console.WriteLine("Publishing TableCloth...");
                await RunCommandAsync("dotnet",
                    $"publish {mainProject} -c {config} -r win-{platform} -p:SelfContained=true " +
                    $"-o {publishDir}");
            }

            // Create Velopack package
            Console.WriteLine();
            Console.WriteLine("Creating Velopack package...");
            var packArgs = new List<string>
            {
                "--yes", "pack",
                "-u", appId,
                "-v", version,
                "-p", publishDir,
                "-e", $"{appId}.exe",
                "-o", releasesDir,
                "--packTitle", appId,
                "--packAuthors", "TableCloth Project",
                "--icon", iconPath,
                // 채널로 아키텍처를 분리해 x64/arm64 의 Setup/Portable/메타데이터
                // 이름이 충돌하지 않게 한다 (build.yml 과 동일).
                "--channel", platform,
            };
            // 코드 서명(--sign): Release 패키지에 한해 Velopack 이 pack 시점에
            // 앱 바이너리 + Update.exe + Setup.exe 를 한 번에 서명한다. signtool 은
            // SimplySign 가상 스마트카드(CurrentUser\My)의 인증서를 /n 으로 선택.
            // signParams 값은 ArgumentList 로 전달되어 OS 수준 따옴표 처리가 자동 적용됨.
            if (doSign && config == "Release")
            {
                packArgs.Add("--signParams");
                packArgs.Add($"/n \"{signSubject}\" /fd sha256 /tr {timestampUrl} /td sha256");
                Console.WriteLine($"  Signing enabled (subject: {signSubject})");
            }
            await RunCommandArgsAsync("vpk", packArgs);

            // Velopack 출력(TableCloth-<arch>-Setup.exe / -Portable.zip)을 릴리스 자산
            // 규칙(TableCloth_<4파트버전>_<config>_<arch>{.exe,_Portable.zip})으로 변경한다.
            // build.yml 의 rename 단계와 동일하므로, (서명된) 산출물을 그대로 GitHub
            // 릴리스에 업로드할 수 있고 winget 자산 매칭과도 호환된다.
            var assetPrefix = $"TableCloth_{projectVersion}_{config}_{platform}";
            RenameRelease(
                Path.Combine(releasesDir, $"TableCloth-{platform}-Setup.exe"),
                Path.Combine(releasesDir, $"{assetPrefix}.exe"));
            RenameRelease(
                Path.Combine(releasesDir, $"TableCloth-{platform}-Portable.zip"),
                Path.Combine(releasesDir, $"{assetPrefix}_Portable.zip"));
        }
    }

    // Open Releases folder
    var releasesPath = Path.Combine(scriptDir, "Releases");
    if (Directory.Exists(releasesPath))
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = releasesPath,
            UseShellExecute = true
        });
    }

    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine($"Build completed. Version: {version} (Commit: {gitCommit})");
    Console.WriteLine("========================================");
}

string GetScriptDirectory()
{
    // Get the directory where this script is located
    var scriptPath = Environment.GetCommandLineArgs()
        .FirstOrDefault(arg => arg.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
    
    if (!string.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
        return Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? Directory.GetCurrentDirectory();
    
    return Directory.GetCurrentDirectory();
}

string GetProjectVersion(string propsPath)
{
    var doc = XDocument.Load(propsPath);
    var ns = doc.Root?.Name.Namespace ?? XNamespace.None;
    
    // Directory.Build.props에서 개별 버전 컴포넌트 읽기
    var major = doc.Descendants(ns + "TableClothVersionMajor").FirstOrDefault()?.Value ?? "1";
    var minor = doc.Descendants(ns + "TableClothVersionMinor").FirstOrDefault()?.Value ?? "0";
    var patch = doc.Descendants(ns + "TableClothVersionPatch").FirstOrDefault()?.Value ?? "0";
    var revision = doc.Descendants(ns + "TableClothVersionRevision").FirstOrDefault()?.Value ?? "0";
    
    return $"{major}.{minor}.{patch}.{revision}";
}


// --key value 형태의 인자에서 value 를 읽는다 (없으면 null).
string? GetArgValue(string key)
{
    var idx = Array.IndexOf(args, key);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

// CurrentUser\My 에 subject 와 일치하고 개인 키를 가진 인증서가 있는지 확인.
bool HasSigningCert(string subject)
{
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
    store.Open(OpenFlags.ReadOnly);
    foreach (var cert in store.Certificates)
    {
        if (cert.HasPrivateKey && cert.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}

// Velopack 출력 파일을 릴리스 자산 이름 규칙으로 이동(rename). src 가 없으면 오류.
void RenameRelease(string src, string dst)
{
    if (!File.Exists(src))
        throw new FileNotFoundException($"Expected Velopack output not found: {src}");
    File.Move(src, dst, overwrite: true);
    Console.WriteLine($"Renamed -> {Path.GetFileName(dst)}");
}

// 단일 문자열 인자 오버로드 (기존 호출부 호환).
async Task<string> RunCommandAsync(string command, string arguments)
{
    var psi = new ProcessStartInfo
    {
        FileName = command,
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    return await RunProcessAsync(psi, command);
}

// 인자 리스트 버전: ArgumentList 가 OS 수준 따옴표/이스케이프를 자동 처리하므로
// 공백·따옴표가 포함된 값(예: --signParams)을 안전하게 전달할 수 있다.
// (지역 함수는 오버로드가 불가하여 이름을 달리한다)
async Task<string> RunCommandArgsAsync(string command, IEnumerable<string> arguments)
{
    var psi = new ProcessStartInfo
    {
        FileName = command,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    foreach (var arg in arguments)
        psi.ArgumentList.Add(arg);
    return await RunProcessAsync(psi, command);
}

async Task<string> RunProcessAsync(ProcessStartInfo startInfo, string command)
{
    using var process = new Process { StartInfo = startInfo };

    var outputBuilder = new System.Text.StringBuilder();

    process.OutputDataReceived += (sender, e) =>
    {
        if (e.Data != null)
        {
            Console.WriteLine(e.Data);
            outputBuilder.AppendLine(e.Data);
        }
    };

    process.ErrorDataReceived += (sender, e) =>
    {
        if (e.Data != null)
        {
            Console.Error.WriteLine(e.Data);
        }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    await process.WaitForExitAsync();

    if (process.ExitCode != 0)
    {
        Console.WriteLine($"Warning: {command} exited with code {process.ExitCode}");
    }

    return outputBuilder.ToString().Trim();
}

#!/usr/bin/env dotnet run

using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;

// Configuration
var appId = "TableCloth";
var solutionFile = "TableCloth.slnx";
var mainProject = Path.Combine("src", "TableCloth", "TableCloth.csproj");
// Spork 단독 배포/재사용 아티팩트 진입점 (TableCloth 와 같은 릴리스에 별도 패키징).
var sporkProject = Path.Combine("src", "Spork", "Spork.csproj");
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

            // === Spork 단독 배포/재사용 아티팩트 (TableCloth 와 동일한 Velopack 형태) ===
            // Spork.csproj 의 조건부 PropertyGroup 이 TableCloth 와 동일하게 단일 파일
            // self-contained 게시를 활성화한다. TableCloth 와 같은 출력 폴더에 함께 두되,
            // 채널을 'spork-<arch>' 로 구분해 메타데이터 이름 충돌을 피한다(같은 릴리스 업로드 가능).
            var sporkPublishDir = Path.Combine("publish", "spork", config, $"win-{platform}");
            var sporkReleasesDir = releasesDir;
            // 단일 바이너리 통합 후 '포카락' 브랜드 폐기 — Spork 패키지/바로가기 아이콘도 식탁보와 동일하게.
            var sporkIcon = iconPath;

            if (!skip)
            {
                Console.WriteLine();
                Console.WriteLine("Publishing Spork...");
                await RunCommandAsync("dotnet",
                    $"publish {sporkProject} -c {config} -r win-{platform} -p:SelfContained=true " +
                    $"-o {sporkPublishDir}");

                // (선택) 무설치/오프라인 폴백용 카탈로그 스냅샷 동봉.
                // Spork.ResourceCacheManager 는 네트워크 실패 시 AppContext.BaseDirectory\catalog\catalog.xml
                // 로 폴백한다. 무설치 시나리오(모드 2 패턴 B)엔 호스트가 주입하는 스냅샷이 없으므로
                // 포터블 산출물에 카탈로그 한 부를 미리 넣어 둔다. external 서브모듈이 없으면(소스 zip
                // 다운로드 등) 조용히 건너뛴다 — 네트워크가 살아 있으면 Spork 는 정상 동작한다.
                var catalogSnapshotSource = Path.Combine("external", "TableClothCatalog", "docs", "Catalog.xml");
                if (File.Exists(catalogSnapshotSource))
                {
                    var catalogSnapshotDir = Path.Combine(sporkPublishDir, "catalog");
                    Directory.CreateDirectory(catalogSnapshotDir);
                    File.Copy(catalogSnapshotSource, Path.Combine(catalogSnapshotDir, "catalog.xml"), overwrite: true);
                    Console.WriteLine($"  Bundled catalog snapshot: {catalogSnapshotSource}");
                }
                else
                {
                    Console.WriteLine($"  (skip) catalog snapshot source missing: {catalogSnapshotSource}");
                }

                // 카탈로그 사이트 아이콘 동봉(오프라인). 단독 Spork 의 ServiceLogoConverter 는
                // AppContext.BaseDirectory\images\{id}.png 를 1순위로 읽으므로, 포터블 산출물에 아이콘을
                // 미리 넣어두면 무설치 환경에서도 첫 화면부터 로컬 아이콘이 즉시 그려진다(원격 다운로드 불필요).
                // 소스는 TableCloth 게시 출력의 Images.zip(우선) 또는 repo 의 src/TableCloth/Images.zip.
                // 변환기가 쓰는 .png 만 추출(.ico 제외). 소스가 없으면 best-effort 로 건너뛴다(원격 폴백 동작).
                var imagesZipSource = new[]
                {
                    Path.Combine(publishDir, "Images.zip"),
                    Path.Combine("src", "TableCloth", "Images.zip"),
                }.FirstOrDefault(File.Exists);

                if (imagesZipSource != null)
                {
                    var sporkImagesDir = Path.Combine(sporkPublishDir, "images");
                    Directory.CreateDirectory(sporkImagesDir);
                    var iconCount = 0;
                    using (var archive = ZipFile.OpenRead(imagesZipSource))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name)) continue;
                            if (!entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                            entry.ExtractToFile(Path.Combine(sporkImagesDir, entry.Name), overwrite: true);
                            iconCount++;
                        }
                    }
                    Console.WriteLine($"  Bundled {iconCount} catalog icons from {imagesZipSource}");
                }
                else
                {
                    Console.WriteLine("  (skip) Images.zip source missing — Spork will load catalog icons remotely.");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Creating Velopack package (Spork)...");
            var sporkPackArgs = new List<string>
            {
                "--yes", "pack",
                "-u", "Spork",
                "-v", version,
                "-p", sporkPublishDir,
                "-e", "Spork.exe",
                "-o", sporkReleasesDir,
                "--packTitle", "TableCloth Spork",
                "--packAuthors", "TableCloth Project",
                "--icon", sporkIcon,
                // 채널을 'spork-<arch>' 로 두어 TableCloth(채널 '<arch>')와 메타데이터
                // (releases.<channel>.json / RELEASES-<channel> / assets.<channel>.json)
                // 이름이 겹치지 않게 한다 — 둘을 같은 GitHub 릴리스에 올릴 수 있어야 하므로.
                "--channel", $"spork-{platform}",
            };
            if (doSign && config == "Release")
            {
                sporkPackArgs.Add("--signParams");
                sporkPackArgs.Add($"/n \"{signSubject}\" /fd sha256 /tr {timestampUrl} /td sha256");
                Console.WriteLine($"  Signing enabled (subject: {signSubject})");
            }
            await RunCommandArgsAsync("vpk", sporkPackArgs);

            // ⚠️ 공개 다운로드 계약(public contract): 아래 포터블 zip 자산명은 무설치 웹앱
            // (yourtablecloth.app)의 다운로더가 의존한다. 변경 시 웹앱이 조용히 깨지므로 신중히.
            // 규칙/예시는 docs/PORTABLE_MODE2_TODO.md 의 "다운로드 자산명 계약" 절 참조.
            var sporkPrefix = $"Spork_{projectVersion}_{config}_{platform}";
            RenameRelease(
                Path.Combine(sporkReleasesDir, $"Spork-spork-{platform}-Setup.exe"),
                Path.Combine(sporkReleasesDir, $"{sporkPrefix}.exe"));
            RenameRelease(
                Path.Combine(sporkReleasesDir, $"Spork-spork-{platform}-Portable.zip"),
                Path.Combine(sporkReleasesDir, $"{sporkPrefix}_Portable.zip"));
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

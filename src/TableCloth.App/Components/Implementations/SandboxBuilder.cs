using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TableCloth.Models.Answers;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class SandboxBuilder(
    IAppMessageBox appMessageBox,
    IArchiveExpander archiveExpander,
    ISharedLocations sharedLocations) : ISandboxBuilder
{
    /// <summary>
    /// 호스트 측 per-session staging 폴더 안에서 TableCloth 바이너리 + 런타임 + 세션 자료가 모이는 leaf 이름.
    /// 이 leaf 이름이 곧 샌드박스 측 노출 경로의 마지막 세그먼트가 되므로
    /// <see cref="SandboxMountPaths.AppDirectory"/>(<c>Desktop\App</c>)와 일치해야 한다.
    /// </summary>
    private const string AppLeafName = "App";

    /// <summary>
    /// framework-dependent 빌드를 샌드박스에서 실행하기 위해 호스트 dotnet 설치 폴더를 노출하는 leaf 이름.
    /// 샌드박스 안에서는 <c>C:\Users\WDAGUtilityAccount\Desktop\dotnet</c>로 노출되며,
    /// StartupScript가 이 경로를 <c>DOTNET_ROOT</c> 환경 변수로 설정한다.
    /// </summary>
    private const string HostDotnetLeafName = "dotnet";

    private static string GetCertificateStagingPathForSandbox()
        => Path.Combine(SandboxMountPaths.AppDirectory, "certs");

    private static string GetNPKIPathForSandbox(X509CertPair certPair)
    {
        // Note: 샌드박스 안에서 사용할 경로를 조립하는 것이므로 SHGetKnownFolderPath API를 사용하면 안됩니다.
        var candidatePath = Path.Join("AppData", "LocalLow", "NPKI", certPair.Organization);

        if (certPair.IsPersonalCert)
            candidatePath = Path.Join(candidatePath, "USER", certPair.SubjectNameForNpkiApp);

        return Path.Join(@"C:\Users\WDAGUtilityAccount", candidatePath);
    }

    public async Task<string?> GenerateSandboxConfigurationAsync(
        string outputDirectory,
        TableClothConfiguration tableClothConfiguration,
        IList<SandboxMappedFolder> excludedDirectories,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        // 통합 단일 바이너리 모델: 호스트 TableCloth 설치 폴더 전체(실행 파일 + 부속 DLL)를
        // 세션 staging의 App 폴더로 복사. 샌드박스는 이 App 폴더를 RO 마운트하여 동일 폴더의
        // TableCloth.exe를 'spork' verb로 실행한다. self-contained 게시물이면 런타임도 함께 복사되므로
        // 별도 처리 불필요. framework-dependent 빌드(개발 단계 기본)면 호스트의 dotnet 설치 폴더를
        // 추가로 RO 마운트하고 StartupScript가 DOTNET_ROOT 환경 변수를 설정해 런타임을 공급한다.
        var appDirectory = Path.Combine(outputDirectory, AppLeafName);
        if (!await CopyTableClothInstallToStagingAsync(sharedLocations.ExecutableDirectoryPath, appDirectory, cancellationToken).ConfigureAwait(false))
            return default;

        tableClothConfiguration.AssetsDirectoryPath = appDirectory;

        // framework-dependent 빌드면 호스트 dotnet 설치에서 host/+shared/(10.*)만 hardlink로 복제한
        // 슬림 미러를 staging 안에 만들고 그것을 마운트 대상으로 사용. 호스트 dotnet 통째 마운트
        // (대표 ~2.5 GB, 그중 1.5 GB는 미사용 SDK)에 비해 마운트 표면이 ~5분의 1로 줄어 sandbox
        // 첫 enumerate 비용 감소.
        if (RequiresHostDotnetMount(appDirectory))
        {
            var hostDotnetRoot = TryResolveHostDotnetRoot();
            tableClothConfiguration.HostDotnetRootPath = hostDotnetRoot is null
                ? null
                : await BuildSlimDotnetMirrorAsync(hostDotnetRoot, outputDirectory, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            tableClothConfiguration.HostDotnetRootPath = null;
        }

        // Spork가 카탈로그 UI에서 사이트 아이콘을 표시하려면 App/images에 png들이 있어야 한다.
        // 호스트 빌드 시 만들어 두는 Images.zip을 그대로 풀어 둔다 (실패해도 catalog 자체는 동작).
        await ExpandImagesZipAsync(sharedLocations.ImagesZipFilePath, appDirectory, cancellationToken).ConfigureAwait(false);

        // 카탈로그 폴백 스냅샷: 샌드박스 내부 네트워크 실패 시 Spork가 사용한다.
        // 호스트의 CatalogCacheFilePath(직전 네트워크 성공 시 호스트가 캐시한 XML)를
        // staging의 catalog 서브폴더로 복사해 둔다. 캐시가 없으면 폴백 없이 진행.
        await CopyCatalogSnapshotAsync(sharedLocations.CatalogCacheFilePath, appDirectory, cancellationToken).ConfigureAwait(false);

        var batchFileContent = GenerateSandboxStartupScript(tableClothConfiguration);
        var batchFilePath = Path.Combine(appDirectory, "StartupScript.cmd");
        await File.WriteAllTextAsync(batchFilePath, batchFileContent, Encoding.Default, cancellationToken).ConfigureAwait(false);

        var sporkAnswerJsonPath = Path.Combine(appDirectory, "SporkAnswers.json");
        var sporkAnswerJsonContent = await SerializeSporkAnswersJsonAsync(new SporkAnswers
        {
            HostUILocale = CultureInfo.CurrentUICulture.Name,

        }, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(sporkAnswerJsonPath, sporkAnswerJsonContent, cancellationToken).ConfigureAwait(false);

        var wsbFilePath = Path.Combine(outputDirectory, "InternetBankingSandbox.wsb");
        var serializedXml = SerializeSandboxSpec(
            await BootstrapSandboxConfigurationAsync(tableClothConfiguration, cancellationToken).ConfigureAwait(false),
            excludedDirectories);
        await File.WriteAllTextAsync(wsbFilePath, serializedXml, cancellationToken).ConfigureAwait(false);

        return wsbFilePath;
    }

    private async Task<SandboxConfiguration> BootstrapSandboxConfigurationAsync(
        TableClothConfiguration tableClothConfig,
        CancellationToken cancellationToken = default)
    {
        const string Enable = "Enable";
        const string Disable = "Disable";

        var sandboxConfig = new SandboxConfiguration
        {
            AudioInput = tableClothConfig.EnableMicrophone ? Enable : Disable,
            VideoInput = tableClothConfig.EnableWebCam ? Enable : Disable,
            PrinterRedirection = tableClothConfig.EnablePrinters ? Enable : Disable,
            VirtualGpu = Disable,
        };

        if (!Directory.Exists(tableClothConfig.AssetsDirectoryPath))
            return sandboxConfig;

        sandboxConfig.MappedFolders.Clear();

        sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
        {
            HostFolder = tableClothConfig.AssetsDirectoryPath,
            ReadOnly = bool.FalseString,
        });

        // framework-dependent 빌드 실행 시 호스트의 dotnet 설치 폴더를 RO로 마운트해 런타임 공급.
        // leaf 이름이 "dotnet"이므로 샌드박스 안에서는 SandboxMountPaths.SandboxDesktop\dotnet로 노출된다.
        if (!string.IsNullOrEmpty(tableClothConfig.HostDotnetRootPath) &&
            Directory.Exists(tableClothConfig.HostDotnetRootPath))
        {
            sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
            {
                HostFolder = tableClothConfig.HostDotnetRootPath,
                ReadOnly = bool.TrueString,
            });
        }

        // 사용자 지정 매핑 폴더 추가
        if (tableClothConfig.MappedFolders != null)
        {
            foreach (var mappedFolder in tableClothConfig.MappedFolders)
            {
                if (!string.IsNullOrWhiteSpace(mappedFolder.HostFolder))
                {
                    sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
                    {
                        HostFolder = mappedFolder.HostFolder,
                        SandboxFolder = mappedFolder.SandboxFolder,
                        ReadOnly = mappedFolder.ReadOnly ? bool.TrueString : bool.FalseString,
                    });
                }
            }
        }

        if (tableClothConfig.CertPair != null &&
            tableClothConfig.CertPair.PublicKey != null &&
            tableClothConfig.CertPair.PrivateKey != null)
        {
            var certStagingDirectoryPath = sharedLocations.GetCertificateStagingDirectoryPath();
            if (Directory.Exists(certStagingDirectoryPath))
                Directory.Delete(certStagingDirectoryPath, true);
            Directory.CreateDirectory(certStagingDirectoryPath);

            var destDerFilePath = Path.Combine(certStagingDirectoryPath, "signCert.der");
            var destKeyFileName = Path.Combine(certStagingDirectoryPath, "signPri.key");

            await File.WriteAllBytesAsync(destDerFilePath, tableClothConfig.CertPair.PublicKey, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(destKeyFileName, tableClothConfig.CertPair.PrivateKey, cancellationToken).ConfigureAwait(false);
        }

        sandboxConfig.LogonCommand.Add(Path.Combine(SandboxMountPaths.AppDirectory, "StartupScript.cmd"));
        return sandboxConfig;
    }

    private string GenerateSandboxStartupScript(TableClothConfiguration tableClothConfiguration)
    {
        ArgumentNullException.ThrowIfNull(tableClothConfiguration);

        var certFileMoveScript = string.Empty;

        if (tableClothConfiguration.CertPair != null)
        {
            var npkiDirectoryPathInSandbox = GetNPKIPathForSandbox(tableClothConfiguration.CertPair);
            var desktopDirectoryPathInSandbox = "%userprofile%\\Desktop\\Certificates";
            var certStagingPath = GetCertificateStagingPathForSandbox();
            var providedCertFilePath = Path.Combine(certStagingPath, "*.*");
            certFileMoveScript = $@"
if not exist ""{npkiDirectoryPathInSandbox}"" mkdir ""{npkiDirectoryPathInSandbox}""
if not exist ""{desktopDirectoryPathInSandbox}"" mkdir ""{desktopDirectoryPathInSandbox}""
copy /y ""{providedCertFilePath}"" ""{npkiDirectoryPathInSandbox}""
move /y ""{providedCertFilePath}"" ""{desktopDirectoryPathInSandbox}""
rmdir /q ""{certStagingPath}""
";
        }

        var switches = new List<string>();

        if (tableClothConfiguration.InstallEveryonesPrinter)
            switches.Add(ConstantStrings.TableCloth_Switch_InstallEveryonesPrinter);

        if (tableClothConfiguration.InstallAdobeReader)
            switches.Add(ConstantStrings.TableCloth_Switch_InstallAdobeReader);

        if (tableClothConfiguration.InstallHancomOfficeViewer)
            switches.Add(ConstantStrings.TableCloth_Switch_InstallHancomOfficeViewer);

        if (tableClothConfiguration.InstallRaiDrive)
            switches.Add(ConstantStrings.TableCloth_Switch_InstallRaiDrive);

        var serviceIdList = (tableClothConfiguration.Services ?? Enumerable.Empty<CatalogInternetService>())
            .Select(x => x.Id).Distinct();
        // 샌드박스 안에서는 호스트 설치 폴더가 그대로 노출된 Desktop\App\TableCloth.exe를 'spork' verb로 실행.
        // verb 디스패처가 'spork' 토큰을 소비하고 나머지 인수를 Spork.App 모듈로 전달한다.
        var tableClothExeInSandbox = Path.Combine(SandboxMountPaths.AppDirectory, "TableCloth.exe");
        var idList = string.Join(" ", serviceIdList);

        // 식탁보 wsb는 <SandboxFolder>를 사용하지 않으므로 호스트의 NPKI 폴더는
        // 데스크톱의 "NPKI" 폴더로 RO 마운트되어 노출된다. 은행/금융 SW가 인식하는 표준 위치
        // (%userprofile%\AppData\LocalLow\NPKI)에는 junction이 아닌 **xcopy**로 독립 사본을
        // 만들어 둔다. 이유:
        //   - junction은 결국 RO 마운트를 가리키므로 호스트 인증서가 그대로 노출되고,
        //     은행 SW가 NPKI에 쓰기 작업(인증서 갱신/캐시 등)을 할 때 실패한다.
        //   - xcopy로 사본을 두면 샌드박스가 독립된 쓰기 가능 NPKI를 갖고, 사본에 대한
        //     수정이 호스트로 새지 않는다.
        // CMD batch의 if-paren 블록은 LogonCommand 컨텍스트에서 파싱이 불안정할 수 있으므로
        // 기존 cert 스크립트와 동일하게 단일 라인 if + goto 패턴을 유지한다.
        var npkiJunctionScript = $@"
if not exist ""{SandboxMountPaths.NpkiDesktopMount}"" goto __tc_skip_npki_copy
if not exist ""%userprofile%\AppData\LocalLow"" mkdir ""%userprofile%\AppData\LocalLow"" >nul 2>&1
xcopy /e /i /q /y ""{SandboxMountPaths.NpkiDesktopMount}"" ""{SandboxMountPaths.NpkiCanonicalPath}"" >nul 2>&1
:__tc_skip_npki_copy";

        // framework-dependent 빌드일 때 호스트 dotnet 마운트가 추가됐다면 DOTNET_ROOT 노출.
        // self-contained 게시물이면 HostDotnetRootPath가 null이므로 set 라인이 들어가지 않는다.
        var dotnetRootScript = string.IsNullOrEmpty(tableClothConfiguration.HostDotnetRootPath)
            ? string.Empty
            : $@"set DOTNET_ROOT={SandboxMountPaths.SandboxDesktop}\{HostDotnetLeafName}
set PATH=%DOTNET_ROOT%;%PATH%";

        return $@"@echo off
pushd ""%~dp0""

REM Configure DNS servers (Google DNS, Cloudflare DNS)
powershell -Command ""Get-NetAdapter | Where-Object {{$_.Status -eq 'Up'}} | Set-DnsClientServerAddress -ServerAddresses ('8.8.8.8','1.1.1.1')"" 2>nul

{dotnetRootScript}
{certFileMoveScript}
{npkiJunctionScript}
""{tableClothExeInSandbox}"" spork {idList} {string.Join(" ", switches)}
:exit
popd
@echo on
";
    }

    /// <summary>
    /// 호스트의 TableCloth 설치 폴더(실행 중인 프로세스의 디렉터리) 전체를 세션 staging의 App 폴더로 복사한다.
    /// 자기 자신을 다시 실행하는 형태이므로 self-contained 게시 출력이어야 .NET 10 런타임이 동봉되어
    /// 샌드박스 안에서 별도 런타임 설치 없이 동작한다.
    /// </summary>
    private async Task<bool> CopyTableClothInstallToStagingAsync(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(sourceDirectory))
            return false;

        try
        {
            if (!Directory.Exists(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);

                // 진입점 폴더에 함께 놓인 카탈로그 이미지 zip은 호스트 빌드 결과물이므로
                // 샌드박스 측에는 풀린 형태(아래 ExpandImagesZipAsync)로 별도 제공한다.
                // 또한 호스트 사용 전용 설정/로그 등이 발견되면 여기서 필터링.
                if (string.Equals(relativePath, "Images.zip", StringComparison.OrdinalIgnoreCase))
                    continue;

                var destinationFile = Path.Combine(destinationDirectory, relativePath);
                var destinationParent = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrEmpty(destinationParent) && !Directory.Exists(destinationParent))
                    Directory.CreateDirectory(destinationParent);

                await CopyFileAsync(sourceFile, destinationFile, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception ex)
        {
            appMessageBox.DisplayError(ex, true);
            return false;
        }
    }

    /// <summary>
    /// 가능한 경우 NTFS 하드링크로 즉시 복제하고, 같은 볼륨이 아니거나 파일 시스템이 하드링크를
    /// 지원하지 않으면 byte 복사로 폴백한다. 하드링크는 거의 비용이 0이고 디스크 사용량을 차지하지
    /// 않으며, 샌드박스의 RO 마운트 측에서는 일반 파일과 구분되지 않는다.
    /// </summary>
    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        if (TryCreateHardLink(destination, source))
            return;

        using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var dst = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await src.CopyToAsync(dst, 81920, cancellationToken).ConfigureAwait(false);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateHardLinkW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    private static bool TryCreateHardLink(string destination, string source)
    {
        try { return CreateHardLinkW(destination, source, IntPtr.Zero); }
        catch { return false; }
    }

    /// <summary>
    /// 호스트의 <c>%ProgramFiles%\dotnet</c>(또는 사용자 설치 경로) 전체(대표 ~2.5 GB, 1.5 GB가 미사용
    /// SDK)를 그대로 마운트하는 대신, 샌드박스에 노출할 최소 슬림 미러를 staging 안에 만든다.
    /// 포함 범위:
    /// <list type="bullet">
    ///   <item><c>host/</c> 전체 — hostfxr.dll 등 ~1.4 MB</item>
    ///   <item><c>shared/Microsoft.NETCore.App/10.*</c> — net10 런타임</item>
    ///   <item><c>shared/Microsoft.WindowsDesktop.App/10.*</c> — net10 WPF/WinForms 런타임</item>
    /// </list>
    /// SDK 폴더와 net6/net8 등 구버전 런타임은 의도적으로 제외. 파일은 가능하면 NTFS 하드링크로
    /// 복제하므로 시간/공간 비용이 거의 0이다.
    /// </summary>
    private static async Task<string?> BuildSlimDotnetMirrorAsync(
        string hostDotnetRoot, string outputDirectory, CancellationToken cancellationToken)
    {
        var slimRoot = Path.Combine(outputDirectory, HostDotnetLeafName);
        if (Directory.Exists(slimRoot))
            Directory.Delete(slimRoot, recursive: true);
        Directory.CreateDirectory(slimRoot);

        var hostSrc = Path.Combine(hostDotnetRoot, "host");
        if (Directory.Exists(hostSrc))
            await MirrorDirectoryAsync(hostSrc, Path.Combine(slimRoot, "host"), cancellationToken).ConfigureAwait(false);

        var sharedSrc = Path.Combine(hostDotnetRoot, "shared");
        if (Directory.Exists(sharedSrc))
        {
            foreach (var sharedApp in new[] { "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App" })
            {
                var appSrc = Path.Combine(sharedSrc, sharedApp);
                if (!Directory.Exists(appSrc))
                    continue;

                foreach (var versionDir in Directory.GetDirectories(appSrc))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var versionName = Path.GetFileName(versionDir);
                    // 본 프로세스가 net10 빌드이므로 10.* 런타임만 미러링. 구버전(6.x/8.x/9.x)은 sandbox에서 사용되지 않음.
                    if (!versionName.StartsWith("10.", StringComparison.Ordinal))
                        continue;

                    var versionDst = Path.Combine(slimRoot, "shared", sharedApp, versionName);
                    await MirrorDirectoryAsync(versionDir, versionDst, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return slimRoot;
    }

    private static async Task MirrorDirectoryAsync(string source, string destination, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(destination))
            Directory.CreateDirectory(destination);

        foreach (var srcFile in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, srcFile);
            var dstFile = Path.Combine(destination, relative);

            var dstParent = Path.GetDirectoryName(dstFile);
            if (!string.IsNullOrEmpty(dstParent) && !Directory.Exists(dstParent))
                Directory.CreateDirectory(dstParent);

            await CopyFileAsync(srcFile, dstFile, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// staging의 App 폴더(또는 게시 출력)에 .NET 호스팅 런타임(<c>hostfxr.dll</c>)이 동봉돼 있는지로
    /// self-contained 여부를 판정한다. 동봉돼 있지 않으면 호스트의 dotnet 설치를 추가 마운트해야 한다.
    /// </summary>
    /// <remarks>
    /// 세 가지 게시 형태를 모두 처리:
    /// <list type="number">
    ///   <item>single-file self-contained — 런타임이 exe 안에 묶여 있음. 호스트 실행 시
    ///         <c>Assembly.Location</c>이 빈 문자열로 보이는 것으로 식별. 추가 마운트 불필요.</item>
    ///   <item>multi-file self-contained — 폴더에 <c>hostfxr.dll</c>이 함께 있음. 추가 마운트 불필요.</item>
    ///   <item>framework-dependent (dev 빌드 기본) — <c>hostfxr.dll</c>이 없음. 호스트 dotnet 마운트 필요.</item>
    /// </list>
    /// </remarks>
    private static bool RequiresHostDotnetMount(string appDirectory)
    {
        if (!Directory.Exists(appDirectory))
            return false;

        // single-file 게시는 진입 어셈블리의 Location이 빈 문자열로 보인다.
        // 이 경우 런타임은 exe 내부에 묶여 있으므로 추가 마운트 불필요.
        var entryAssemblyLocation = typeof(SandboxBuilder).Assembly.Location;
        if (string.IsNullOrEmpty(entryAssemblyLocation))
            return false;

        // multi-file 형태: hostfxr.dll이 같은 폴더에 있으면 self-contained로 판정.
        var hostfxr = Path.Combine(appDirectory, "hostfxr.dll");
        return !File.Exists(hostfxr);
    }

    /// <summary>
    /// 호스트 측 .NET 설치 루트(<c>dotnet</c> 폴더)를 우선순위에 따라 탐색해 반환한다.
    /// DOTNET_ROOT 환경 변수 → 시스템 설치(<c>%ProgramFiles%\dotnet</c>) → 사용자 설치
    /// (<c>%LocalAppData%\Microsoft\dotnet</c>) 순으로 시도한다.
    /// 어느 경로도 유효하지 않으면 <see langword="null"/>을 반환한다.
    /// </summary>
    private static string? TryResolveHostDotnetRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
            return fromEnv;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            var systemInstall = Path.Combine(programFiles, "dotnet");
            if (Directory.Exists(systemInstall))
                return systemInstall;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var userInstall = Path.Combine(localAppData, "Microsoft", "dotnet");
            if (Directory.Exists(userInstall))
                return userInstall;
        }

        return null;
    }

    private async Task ExpandImagesZipAsync(string imagesZipFilePath, string assetsDirectory, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagesZipFilePath))
            return;

        var imagesTargetDirectory = Path.Combine(assetsDirectory, "images");

        if (!Directory.Exists(imagesTargetDirectory))
            Directory.CreateDirectory(imagesTargetDirectory);

        try
        {
            await archiveExpander.ExpandArchiveAsync(
                imagesZipFilePath, imagesTargetDirectory, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 아이콘이 없어도 카탈로그 UI는 동작해야 하므로 실패는 조용히 넘긴다.
        }
    }

    private static async Task CopyCatalogSnapshotAsync(string sourcePath, string assetsDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            return;

        try
        {
            var snapshotDirectory = Path.Combine(assetsDirectory, "catalog");
            if (!Directory.Exists(snapshotDirectory))
                Directory.CreateDirectory(snapshotDirectory);

            var destinationPath = Path.Combine(snapshotDirectory, "catalog.xml");

            using (var source = File.OpenRead(sourcePath))
            using (var destination = File.Create(destinationPath))
            {
                await source.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // 스냅샷 주입 실패가 샌드박스 시작을 막아서는 안 된다. Spork는 네트워크가 살아 있으면 정상 동작한다.
        }
    }

    public static async Task<string> SerializeSporkAnswersJsonAsync(SporkAnswers answers, CancellationToken cancellationToken = default)
    {
        using var memStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memStream, answers, new JsonSerializerOptions() { WriteIndented = true, }, cancellationToken).ConfigureAwait(false);
        return new UTF8Encoding(false).GetString(memStream.ToArray());
    }

    private static string SerializeSandboxSpec(SandboxConfiguration configuration, IList<SandboxMappedFolder> excludedFolders)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var unavailableDirectories = configuration.MappedFolders
            .Where(x => !Directory.Exists(x.HostFolder))
            .ToList();

        configuration.MappedFolders.RemoveAll(x => unavailableDirectories.Contains(x));

        if (excludedFolders != null)
        {
            foreach (var eachDirectory in unavailableDirectories)
                excludedFolders.Add(eachDirectory);
        }

        var configElement = new XElement("Configuration");

        AddElementIfNotNull(configElement, "Networking", configuration.Networking);
        AddElementIfNotNull(configElement, "AudioInput", configuration.AudioInput);
        AddElementIfNotNull(configElement, "VideoInput", configuration.VideoInput);
        AddElementIfNotNull(configElement, "vGPU", configuration.VirtualGpu);
        AddElementIfNotNull(configElement, "PrinterRedirection", configuration.PrinterRedirection);
        AddElementIfNotNull(configElement, "ClipboardRedirection", configuration.ClipboardRedirection);
        AddElementIfNotNull(configElement, "ProtectedClient", configuration.ProtectedClient);

        if (configuration.MemoryInMB.HasValue)
            configElement.Add(new XElement("MemoryInMB", configuration.MemoryInMB.Value));

        if (configuration.MappedFolders.Count > 0)
        {
            var mappedFoldersElement = new XElement("MappedFolders");
            foreach (var folder in configuration.MappedFolders)
            {
                var folderElement = new XElement("MappedFolder",
                    new XElement("HostFolder", folder.HostFolder));

                AddElementIfNotNull(folderElement, "SandboxFolder", folder.SandboxFolder);
                AddElementIfNotNull(folderElement, "ReadOnly", folder.ReadOnly);

                mappedFoldersElement.Add(folderElement);
            }
            configElement.Add(mappedFoldersElement);
        }

        if (configuration.LogonCommand.Count > 0)
        {
            var logonCommandElement = new XElement("LogonCommand");
            foreach (var command in configuration.LogonCommand)
            {
                logonCommandElement.Add(new XElement("Command", command));
            }
            configElement.Add(logonCommandElement);
        }

        var doc = new XDocument(configElement);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static void AddElementIfNotNull(XElement parent, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            parent.Add(new XElement(name, value));
    }
}

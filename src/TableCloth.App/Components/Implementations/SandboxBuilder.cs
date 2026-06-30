using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// 인증서 staging은 App 디렉터리 하위 <c>certs</c>로 통일. App 디렉터리가 그대로 샌드박스
    /// <c>Desktop\App</c>으로 노출되므로, 호스트가 여기 쓴 파일은 추가 마운트 없이 그대로 보인다.
    /// 샌드박스 진입 후 Spork.App의 ISandboxBootstrap이 NPKI 표준 경로로 옮긴다.
    /// </summary>
    private static string GetCertificateStagingPathOnHost(string appDirectory)
        => Path.Combine(appDirectory, "certs");

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
        tableClothConfiguration.HostDotnetRootPath = RequiresHostDotnetMount(appDirectory)
            ? TryResolveHostDotnetRoot()
            : null;

        // Spork가 카탈로그 UI에서 사이트 아이콘을 표시하려면 App/images에 png들이 있어야 한다.
        // 호스트 빌드 시 만들어 두는 Images.zip을 그대로 풀어 둔다 (실패해도 catalog 자체는 동작).
        await ExpandImagesZipAsync(sharedLocations.ImagesZipFilePath, appDirectory, cancellationToken).ConfigureAwait(false);

        // 카탈로그 폴백 스냅샷: 샌드박스 내부 네트워크 실패 시 Spork가 사용한다.
        // 호스트의 CatalogCacheFilePath(직전 네트워크 성공 시 호스트가 캐시한 XML)를
        // staging의 catalog 서브폴더로 복사해 둔다. 캐시가 없으면 폴백 없이 진행.
        await CopyCatalogSnapshotAsync(sharedLocations.CatalogCacheFilePath, appDirectory, cancellationToken).ConfigureAwait(false);

        // 인증서가 있으면 App\certs 하위로 떨궈둔다. App 폴더가 그대로 샌드박스 Desktop\App로
        // 노출되므로 추가 마운트 없이 Spork가 AppContext.BaseDirectory\certs에서 그대로 읽는다.
        var sporkAnswers = new SporkAnswers
        {
            HostUILocale = CultureInfo.CurrentUICulture.Name,
            // 호스트의 라이트/다크 선호를 함께 실어 보내, 샌드박스 부팅 시 시작 시점 테마를 맞춘다(이슈 #246).
            HostUsesLightTheme = DetectHostUsesLightTheme(),
        };
        await StageCertPairAsync(appDirectory, tableClothConfiguration.CertPair, sporkAnswers, cancellationToken).ConfigureAwait(false);

        var batchFileContent = GenerateSandboxStartupScript(tableClothConfiguration);
        var batchFilePath = Path.Combine(appDirectory, "StartupScript.cmd");
        await File.WriteAllTextAsync(batchFilePath, batchFileContent, Encoding.Default, cancellationToken).ConfigureAwait(false);

        var sporkAnswerJsonPath = Path.Combine(appDirectory, "SporkAnswers.json");
        var sporkAnswerJsonContent = await SerializeSporkAnswersJsonAsync(sporkAnswers, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(sporkAnswerJsonPath, sporkAnswerJsonContent, cancellationToken).ConfigureAwait(false);

        var wsbFilePath = Path.Combine(outputDirectory, "InternetBankingSandbox.wsb");
        var serializedXml = SerializeSandboxSpec(
            BootstrapSandboxConfiguration(tableClothConfiguration),
            excludedDirectories);
        await File.WriteAllTextAsync(wsbFilePath, serializedXml, cancellationToken).ConfigureAwait(false);

        return wsbFilePath;
    }

    /// <summary>
    /// 호스트의 라이트/다크 선호를 <c>HKCU\...\Themes\Personalize\AppsUseLightTheme</c>에서 읽는다.
    /// (VisualThemeManager가 자기 UI 테마 판별에 쓰는 것과 동일한 값.) 키가 없거나 읽기에 실패하면
    /// <see langword="null"/>(알 수 없음)을 반환해 샌드박스 테마를 강제로 바꾸지 않게 한다.
    /// </summary>
    private static bool? DetectHostUsesLightTheme()
    {
        try
        {
            using var personalizeKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);

            if (personalizeKey?.GetValue("AppsUseLightTheme") is int appsUseLightTheme)
                return appsUseLightTheme > 0;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static SandboxConfiguration BootstrapSandboxConfiguration(
        TableClothConfiguration tableClothConfig)
    {
        const string Enable = "Enable";
        const string Disable = "Disable";

        // vGPU 기본값은 Disable. 호스트 GPU 공유는 사용자가 명시적으로 옵션을 켤 때만 활성화한다.
        // - Disable: 샌드박스에 호스트 GPU 어댑터가 전혀 노출되지 않음. 소프트웨어 렌더링(WARP)으로 동작하므로
        //   특정 NVIDIA 드라이버 등에서 보고된 Edge 흰 화면 같은 비결정적 GPU 경로 문제를 회피한다.
        // - Default: WSB가 호스트 OS 기본 정책에 따라 vGPU를 공유한다(보통 호스트 GPU 어댑터 노출).
        //   GPU 가속이 필요한 시각화/미디어 작업 등에서만 켠다.
        var sandboxConfig = new SandboxConfiguration
        {
            AudioInput = tableClothConfig.EnableMicrophone ? Enable : Disable,
            VideoInput = tableClothConfig.EnableWebCam ? Enable : Disable,
            PrinterRedirection = tableClothConfig.EnablePrinters ? Enable : Disable,
            VirtualGpu = tableClothConfig.EnableSandboxGpuAcceleration ? "Default" : Disable,
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

        sandboxConfig.LogonCommand.Add(Path.Combine(SandboxMountPaths.AppDirectory, "StartupScript.cmd"));
        return sandboxConfig;
    }

    /// <summary>
    /// 호스트가 가진 인증서 쌍을 staging의 <c>App\certs</c>로 떨어뜨리고, NPKI 경로 조립에 필요한
    /// 식별자(O / SubjectNameForNpkiApp / IsPersonalCert)를 <see cref="SporkAnswers"/>에 채워 넣는다.
    /// 실제 배치(NPKI 표준 경로 + Desktop\Certificates 복사)는 샌드박스 안에서 Spork.App이 처리한다.
    /// </summary>
    private static async Task StageCertPairAsync(
        string appDirectory,
        X509CertPair? certPair,
        SporkAnswers sporkAnswers,
        CancellationToken cancellationToken)
    {
        if (certPair?.PublicKey == null || certPair.PrivateKey == null)
            return;

        var certStagingDirectoryPath = GetCertificateStagingPathOnHost(appDirectory);
        if (Directory.Exists(certStagingDirectoryPath))
            Directory.Delete(certStagingDirectoryPath, true);
        Directory.CreateDirectory(certStagingDirectoryPath);

        var destDerFilePath = Path.Combine(certStagingDirectoryPath, "signCert.der");
        var destKeyFileName = Path.Combine(certStagingDirectoryPath, "signPri.key");

        await File.WriteAllBytesAsync(destDerFilePath, certPair.PublicKey, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(destKeyFileName, certPair.PrivateKey, cancellationToken).ConfigureAwait(false);

        sporkAnswers.HasCertPair = true;
        sporkAnswers.CertOrganization = certPair.Organization;
        sporkAnswers.CertIsPersonalCert = certPair.IsPersonalCert;
        sporkAnswers.CertSubjectNameForNpkiApp = certPair.SubjectNameForNpkiApp;
    }

    private string GenerateSandboxStartupScript(TableClothConfiguration tableClothConfiguration)
    {
        ArgumentNullException.ThrowIfNull(tableClothConfiguration);

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

        // framework-dependent 빌드일 때 호스트 dotnet 마운트가 추가됐다면 DOTNET_ROOT 노출.
        // - set: 현 batch 프로세스 트리(LogonCommand 흐름)에서 즉시 사용. .NET 호스트가 exe를 띄우기
        //   *전에* 설정되어야 하므로 batch 에 남긴다. self-contained 게시물이면 HostDotnetRootPath 가
        //   null 이므로 본 라인이 들어가지 않는다.
        // - setx: 사용자 단위(HKCU\Environment) 영구 등록 + WM_SETTINGCHANGE 브로드캐스트. 데스크톱
        //   바로가기로 새로 띄우는 인스턴스(LogonCommand 흐름 밖)도 DOTNET_ROOT 를 상속받아 dev 빌드의
        //   재실행 시나리오가 동작한다. sandbox VHD 안 HKCU 라 호스트 머신엔 영향 없음. stdout 의
        //   "SUCCESS: ..." 메시지는 nul 로 흡수.
        var dotnetRootScript = string.IsNullOrEmpty(tableClothConfiguration.HostDotnetRootPath)
            ? string.Empty
            : $@"set DOTNET_ROOT={SandboxMountPaths.SandboxDesktop}\{HostDotnetLeafName}
set PATH=%DOTNET_ROOT%;%PATH%
setx DOTNET_ROOT ""{SandboxMountPaths.SandboxDesktop}\{HostDotnetLeafName}"" >nul 2>&1
";

        // 부팅 직후 LogonCommand는 powershell.exe 콜드 스타트 + 모듈 로드로만 수 초가 소모된다.
        // 따라서 batch는 환경 변수 + exe 실행만 담당하고, 나머지(DNS 설정, NPKI 복사, 인증서 배치)는
        // Spork.App 내부 ISandboxBootstrap이 UI 진입 시점에 직접 처리한다.
        //
        // 예외: Smart App Control(SAC) 비활성화는 *반드시* TableCloth.exe 실행 전에 끝나야 한다.
        // citool --refresh가 호출되면 커널 CI가 실행 중인 모든 프로세스를 새 정책으로 재평가하는데,
        // single-file + EV 미서명 상태의 TableCloth.exe는 untrusted로 분류되어 강제 종료된다.
        // reg.exe + citool.exe는 System32의 EV 서명 바이너리라 SAC가 신뢰하므로 batch 단계에서
        // 호출하는 것이 안전.
        //
        // GPU 가속 OFF(기본)일 때 vGPU Disable 만으로는 부족할 수 있어 Edge 정책도 함께 적용해
        // 다층 차단한다. Edge는 vGPU가 없어도 WARP/소프트웨어 GPU 경로를 시도하며, 이 경로가
        // 일부 환경에서 흰 화면이나 렌더링 지연을 유발할 수 있기 때문에 정책 키로 명시적으로
        // 하드웨어 가속을 끄는 것이 가장 안전하다. msedge.exe가 처음 뜨기 전에 정책이 들어가야
        // 새 프로세스가 정책을 읽기 때문에 batch 단계에서 적용한다(LogonCommand 시점엔 Edge가
        // 아직 없으므로 재시작 무관).
        var disableEdgeGpuScript = !tableClothConfiguration.EnableSandboxGpuAcceleration
            ? @"reg add ""HKLM\SOFTWARE\Policies\Microsoft\Edge"" /v HardwareAccelerationModeEnabled /t REG_DWORD /d 0 /f >nul 2>&1
"
            : string.Empty;

        return $@"@echo off
pushd ""%~dp0""
{dotnetRootScript}reg add ""HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy"" /v VerifiedAndReputablePolicyState /t REG_DWORD /d 0 /f >nul 2>&1
{disableEdgeGpuScript}""%SystemRoot%\System32\citool.exe"" --refresh >nul 2>&1
""{tableClothExeInSandbox}"" spork {idList} {string.Join(" ", switches)}
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

    private static async Task CopyFileAsync(string source, string destination, CancellationToken cancellationToken)
    {
        using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var dst = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await src.CopyToAsync(dst, 81920, cancellationToken).ConfigureAwait(false);
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

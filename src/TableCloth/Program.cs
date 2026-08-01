using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Win32;
using Spork.App.DependencyInjection;
using Spork.Sandbox;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using TableCloth.App.DependencyInjection;
using TableCloth.Bootstrap.Dialogs;
using TableCloth.Components.Implementations;
using TableCloth.Models;
using TableCloth.Models.Configuration;
using TableCloth.Resources;
using Velopack;

namespace TableCloth;

internal static class Program
{
    private const string SporkVerb = "spork";

    // [미리 보기] 유휴 자동 종료(이슈 #197) 전용 헤드리스 프로세스. StartupScript가 Spork 런처와 별개로
    // 기동하여, 창을 닫아도 유휴 보호가 유지되게 한다.
    private const string IdleGuardVerb = "idle-guard";

    [STAThread]
    private static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            MessageBox.Show(
                e.ExceptionObject?.ToString() ?? "Unknown Error",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        args ??= Helpers.GetCommandLineArguments();

        // verb 디스패치: 첫 토큰이 `spork`이면 Spork 모듈로 라우팅, 그렇지 않으면 TableCloth 호스트 모드.
        // System.CommandLine으로 감싸지 않고 단순 분기를 사용한 이유는, 두 모듈이 각자 자체
        // CommandLineArguments(System.CommandLine RootCommand)로 옵션을 파싱하기 때문. 디스패처는
        // 단지 verb 토큰을 소비하고 남은 인수를 Helpers.SetEffectiveCommandLineArguments로 노출한다.
        if (args.Length > 0 && string.Equals(args[0], SporkVerb, StringComparison.OrdinalIgnoreCase))
            return RunSpork(args.Skip(1).ToArray());

        if (args.Length > 0 && string.Equals(args[0], IdleGuardVerb, StringComparison.OrdinalIgnoreCase))
            return RunIdleGuard(args.Skip(1).ToArray());

        return RunTableCloth(args);
    }

    // [미리 보기] 유휴 자동 종료 가드(이슈 #197). 메인 창 없는 최소 WPF 앱을 띄워 유휴 모니터만 돌린다.
    // 헤드리스 헬퍼이므로 실패해도 대화상자를 띄우지 않고 조용히 종료한다.
    private static int RunIdleGuard(string[] args)
    {
        try
        {
            var app = new Spork.IdleGuardApplication();
            app.Run();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[idle-guard] fatal: {ex}");
        }

        return Environment.ExitCode;
    }

    private static int RunTableCloth(string[] args)
    {
        // Velopack 초기화 - 설치/업데이트/제거 시 처리
        VelopackApp.Build().Run();

        // 라이선스 동의 여부 확인
        if (!CheckLicenseAgreement())
        {
            Environment.Exit(1); // 라이선스 미동의 시 종료
            return Environment.ExitCode;
        }

        // 설치 후 첫 실행 시 파일 연결 등록
        RegisterFileAssociationsIfNeeded();
        RegisterUriSchemeIfNeeded();

        // tablecloth: 딥링크로 실행됐다면, 이미 떠 있는 인스턴스에 넘기고 조용히 종료한다.
        // 넘길 상대가 없으면 이 프로세스가 그 인스턴스가 되고, 페이로드는 정규 인자로 바뀐다.
        args = ResolveDeepLinkArguments(args, out var handedOverToRunningInstance);

        if (handedOverToRunningInstance)
            return 0;

        try
        {
            Helpers.SetEffectiveCommandLineArguments(args);

            var builder = Host.CreateApplicationBuilder(args);
            builder.UseTableCloth();

            using var appHost = builder.Build();
            appHost.Start();
            var app = appHost.Services.GetRequiredService<Application>();
            app.Run();
            appHost.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex?.ToString() ?? "Unknown Error",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Environment.ExitCode;
    }

    private static int RunSpork(string[] args)
    {
        try
        {
            // 모듈의 CommandLineArguments는 Helpers.GetCommandLineArguments()를 호출해 인수를 읽는다.
            // verb 토큰('spork')이 소비된 뒤의 인수만 모듈에 노출되도록 명시.
            Helpers.SetEffectiveCommandLineArguments(args);

            var builder = Host.CreateApplicationBuilder(args);
            builder.UseSpork();
            // UseSpork()가 등록한 ISandboxBootstrap의 noop 기본 구현을 실제 sandbox 구현으로 교체.
            // 본 호출은 TableCloth.exe(통합 진입점)에서만 일어나며, 단독 Spork.exe는 Spork.Sandbox를
            // 참조하지 않으므로 noop 그대로 사용된다.
            builder.UseSandboxBootstrap();

            using var appHost = builder.Build();
            appHost.Start();
            var app = appHost.Services.GetRequiredService<Application>();
            app.Run();
            appHost.StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex?.ToString() ?? "Unknown Error",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Environment.ExitCode;
    }

    private static bool CheckLicenseAgreement()
    {
        try
        {
            var preferencesPath = GetPreferencesFilePath();
            if (!File.Exists(preferencesPath))
            {
                // 설정 파일이 없으면 라이선스 동의 필요
                return ShowLicenseAgreement();
            }

            var json = File.ReadAllText(preferencesPath);
            var preferences = JsonSerializer.Deserialize<PreferenceSettings>(json);

            if (preferences?.LicenseAgreedTime == null)
            {
                // 라이선스 동의 기록이 없으면 동의 필요
                return ShowLicenseAgreement();
            }

            return true;
        }
        catch (Exception ex)
        {
            // 설정 파일 손상/권한 등으로 읽기에 실패하면 동의 창을 다시 띄운다.
            // 부트스트랩 단계라 DI 로거가 아직 없으므로 Debug 출력에만 남긴다.
            Debug.WriteLine($"[TableCloth] CheckLicenseAgreement failed: {ex}");
            return ShowLicenseAgreement();
        }
    }

    private static bool ShowLicenseAgreement()
    {
        var licenseWindow = new LicenseWindow();
        var result = licenseWindow.ShowDialog();

        if (result == true && licenseWindow.LicenseAccepted)
        {
            // 라이선스 동의 정보 저장
            SaveLicenseAgreement();
            return true;
        }
        else
        {
            // 라이선스 거부 시 메시지 표시
            MessageBox.Show(
                UIStringResources.License_RejectionMessage,
                UIStringResources.License_RejectionTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }
    }

    private static void SaveLicenseAgreement()
    {
        try
        {
            var preferencesPath = GetPreferencesFilePath();
            var preferencesDir = Path.GetDirectoryName(preferencesPath);

            if (!string.IsNullOrEmpty(preferencesDir) && !Directory.Exists(preferencesDir))
                Directory.CreateDirectory(preferencesDir);

            PreferenceSettings preferences;

            if (File.Exists(preferencesPath))
            {
                var json = File.ReadAllText(preferencesPath);
                preferences = JsonSerializer.Deserialize<PreferenceSettings>(json) ?? new PreferenceSettings();
            }
            else
            {
                preferences = new PreferenceSettings();
            }

            preferences.LicenseAgreedTime = DateTime.UtcNow;
            preferences.LicenseAgreedVersion = typeof(Program).Assembly.GetName().Version?.ToString();

            var options = new JsonSerializerOptions { WriteIndented = true };
            var updatedJson = JsonSerializer.Serialize(preferences, options);
            File.WriteAllText(preferencesPath, updatedJson);
        }
        catch (Exception ex)
        {
            // 저장 실패는 무시 - 다음 실행 시 다시 동의 요청한다.
            // 부트스트랩 단계라 DI 로거가 아직 없으므로 Debug 출력에만 남긴다.
            Debug.WriteLine($"[TableCloth] SaveLicenseAgreement failed: {ex}");
        }
    }

    private static string GetPreferencesFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "TableCloth.Data", "Preferences.json");
    }

    /// <summary>
    /// 딥링크로 실행된 경우의 진입 처리. 실행 중인 인스턴스가 있으면 페이로드를 넘기고,
    /// 없으면 페이로드를 이 앱이 이미 이해하는 정규 인자로 바꾼다.
    /// </summary>
    /// <remarks>
    /// 페이로드는 임의의 웹 페이지가 넣는 값이므로 <b>원문 그대로 argv 에 흘리지 않는다</b>.
    /// 이 앱의 인자 파이프라인은 <c>@파일</c> 응답 파일 확장을 쓰고 있어(바탕화면 `.tclnk` 연결이 그
    /// 기능에 의존한다) 페이로드가 argv 로 들어가면 임의 경로(UNC 포함) 파일 읽기가 되기 때문이다.
    /// <see cref="TableClothUri"/> 파서를 통과한 값만 정규 인자로 재구성한다.
    /// </remarks>
    private static string[] ResolveDeepLinkArguments(string[] args, out bool handedOverToRunningInstance)
    {
        handedOverToRunningInstance = false;

        var payload = ExtractDeepLinkPayload(args);

        if (payload == null)
            return args;

        if (DeepLinkActivationChannel.TrySend(payload))
        {
            handedOverToRunningInstance = true;
            return args;
        }

        // 해석에 실패한 딥링크는 인자 없이 평소처럼 앱을 연다(요청을 무시하는 쪽이 안전한 기본값).
        return TableClothUri.TryParse(payload, out var request)
            ? request.ToCanonicalArguments()
            : Array.Empty<string>();
    }

    /// <summary>
    /// 명령줄에서 딥링크 페이로드를 찾는다. 등록된 처리기는 <c>--uri "%1"</c> 형태로 넘기지만,
    /// 예전 등록이나 수동 실행처럼 페이로드만 오는 경우도 받아준다.
    /// </summary>
    private static string? ExtractDeepLinkPayload(string[] args)
    {
        if (args == null || args.Length < 1)
            return null;

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], ConstantStrings.TableCloth_Switch_Uri, StringComparison.OrdinalIgnoreCase))
                continue;

            return i + 1 < args.Length ? args[i + 1] : null;
        }

        return TableClothUri.IsDeepLink(args[0]) ? args[0] : null;
    }

    /// <summary>
    /// <c>tablecloth:</c> URI 스킴 처리기를 현재 사용자에게 등록한다.
    /// </summary>
    /// <remarks>
    /// <c>HKCU\Software\Classes</c> 등록이라 관리자 권한이 필요 없다(파일 연결과 같은 방식).
    /// 실행 파일 경로가 바뀐 경우(재설치 등)를 위해 기존 등록과 다르면 갱신한다.
    /// </remarks>
    private static void RegisterUriSchemeIfNeeded()
    {
        try
        {
            // 파일 연결과 같은 기준: Velopack 으로 설치된 경우에만 등록한다. 포터블/개발 실행이
            // 임시 경로를 전역 스킴으로 등록해버리는 것을 막는다.
            var updateManager = new UpdateManager(new Velopack.Sources.GithubSource(
                "https://github.com/yourtablecloth/TableCloth", null, false));

            if (!updateManager.IsInstalled)
                return;

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            var expectedCommand = $"\"{exePath}\" {ConstantStrings.TableCloth_Switch_Uri} \"%1\"";

            using var classesKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            if (classesKey == null) return;

            using (var existingCommandKey = classesKey.OpenSubKey($@"{TableClothUri.SchemeName}\shell\open\command"))
            {
                if (existingCommandKey?.GetValue(null) is string existingCommand &&
                    string.Equals(existingCommand, expectedCommand, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            using var schemeKey = classesKey.CreateSubKey(TableClothUri.SchemeName);
            if (schemeKey == null) return;

            // "URL Protocol" 값의 존재 자체가 이 ProgId 를 URI 스킴 처리기로 표시한다(값은 빈 문자열).
            schemeKey.SetValue("", "URL:TableCloth Protocol");
            schemeKey.SetValue("URL Protocol", string.Empty);

            using (var iconKey = schemeKey.CreateSubKey("DefaultIcon"))
            {
                iconKey?.SetValue("", $"\"{exePath}\",0");
            }

            using (var commandKey = schemeKey.CreateSubKey(@"shell\open\command"))
            {
                // "%1" 의 큰따옴표는 필수다. 없으면 셸이 공백으로 인자를 쪼개 스위치 주입이 가능해진다.
                commandKey?.SetValue("", expectedCommand);
            }
        }
        catch (Exception ex)
        {
            // 등록 실패는 딥링크만 동작하지 않게 할 뿐 앱 실행에는 영향이 없다.
            // 부트스트랩 단계라 DI 로거가 아직 없으므로 Debug 출력에만 남긴다.
            Debug.WriteLine($"[TableCloth] RegisterUriSchemeIfNeeded failed: {ex}");
        }
    }

    private static void RegisterFileAssociationsIfNeeded()
    {
        try
        {
            // Velopack으로 설치된 경우에만 파일 연결 등록
            var updateManager = new UpdateManager(new Velopack.Sources.GithubSource(
                "https://github.com/yourtablecloth/TableCloth", null, false));

            if (!updateManager.IsInstalled)
                return;

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            using var classesKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            if (classesKey == null) return;

            // 이미 등록되어 있는지 확인
            using var existingKey = classesKey.OpenSubKey("TableCloth.tclnk");
            if (existingKey != null) return;

            // .tclnk 확장자 등록
            using (var extKey = classesKey.CreateSubKey(".tclnk"))
            {
                extKey?.SetValue("", "TableCloth.tclnk");
                using var progIdsKey = extKey?.CreateSubKey("OpenWithProgids");
                progIdsKey?.SetValue("TableCloth.tclnk", "");
            }

            // TableCloth.tclnk ProgId 등록
            using (var progIdKey = classesKey.CreateSubKey("TableCloth.tclnk"))
            {
                progIdKey?.SetValue("", "TableCloth Link File");

                using (var iconKey = progIdKey?.CreateSubKey("DefaultIcon"))
                {
                    iconKey?.SetValue("", $"\"{exePath}\",0");
                }

                using (var commandKey = progIdKey?.CreateSubKey(@"shell\open\command"))
                {
                    commandKey?.SetValue("", $"\"{exePath}\" \"@%1\"");
                }
            }
        }
        catch (Exception ex)
        {
            // 레지스트리 권한/잠금 등으로 등록에 실패하면 .tclnk 더블클릭이 동작하지
            // 않으나 앱 실행 자체에는 영향을 주지 않는다. 부트스트랩 단계라 DI
            // 로거가 아직 없으므로 Debug 출력에만 남긴다.
            Debug.WriteLine($"[TableCloth] RegisterFileAssociationsIfNeeded failed: {ex}");
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.App.DependencyInjection;
using TableCloth.Bootstrap.Dialogs;
using TableCloth.Components;
using TableCloth.Components.Implementations;
using TableCloth.Dialogs;
using TableCloth.Models.Configuration;
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.ViewModels;
using Velopack;

namespace TableCloth;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
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

        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                MessageBox.Show(
                    e.ExceptionObject?.ToString() ?? "Unknown Error",
                    "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            args ??= Helpers.GetCommandLineArguments();
            var builder = Host.CreateApplicationBuilder(args);

            // Phase 1 — TableCloth.App 모듈 합성 진입점 확립.
            // 현재는 no-op이며, Components/ViewModels/Views를 점진 이전하면서 채워진다.
            builder.UseTableCloth();

            builder.Logging
                .AddSerilog(dispose: true)
                .AddConsole();

            // Add Logging
            builder.Services.AddLogging();

            // Add HTTP Service
            builder.Services.AddHttpClient(
                nameof(ConstantStrings.UserAgentText),
                c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText));
            builder.Services.AddHttpClient(
                nameof(ConstantStrings.FamiliarUserAgentText),
                c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.FamiliarUserAgentText));
            builder.Services.AddHttpClient(
                nameof(StringResources.TableCloth_GitHubRestUAString),
                c => c.DefaultRequestHeaders.Add("User-Agent", StringResources.TableCloth_GitHubRestUAString));

            // Add Components
            builder.Services
                .AddSingleton<IAppUserInterface, AppUserInterface>()
                .AddSingleton<IAppUpdateManager, AppUpdateManager>()
                .AddSingleton<ISharedLocations, SharedLocations>()
                .AddSingleton<IPreferencesManager, PreferencesManager>()
                .AddSingleton<IX509CertPairScanner, X509CertPairScanner>()
                .AddSingleton<IResourceCacheManager, ResourceCacheManager>()
                .AddSingleton<ISandboxBuilder, SandboxBuilder>()
                .AddSingleton<ISandboxLauncher, SandboxLauncher>()
                .AddSingleton<ISandboxCleanupManager, SandboxCleanupManager>()
                .AddSingleton<IAppStartup, AppStartup>()
                .AddSingleton<IResourceResolver, ResourceResolver>()
                .AddSingleton<ILicenseDescriptor, LicenseDescriptor>()
                .AddSingleton<IAppRestartManager, AppRestartManager>()
                .AddSingleton<ICommandLineComposer, CommandLineComposer>()
                .AddSingleton<IConfigurationComposer, ConfigurationComposer>()
                .AddSingleton<IVisualThemeManager, VisualThemeManager>()
                .AddSingleton<IAppMessageBox, AppMessageBox>()
                .AddSingleton<IMessageBoxService, MessageBoxService>()
                .AddSingleton<INavigationService, NavigationService>()
                .AddSingleton<IShortcutCreator, ShortcutCreator>()
                .AddSingleton<ICommandLineArguments, CommandLineArguments>()
                .AddSingleton<IApplicationService, ApplicationService>()
                .AddSingleton<IArchiveExpander, ArchiveExpander>()
                .AddSingleton<ICatalogDeserializer, CatalogDeserializer>()
                .AddSingleton(_ => new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext()));

            // UI
            builder.Services
                .AddWindow<DisclaimerWindow, DisclaimerWindowViewModel>()
                .AddWindow<InputPasswordWindow, InputPasswordWindowViewModel>()
                .AddWindow<AboutWindow, AboutWindowViewModel>()
                .AddWindow<OptionsWindow, OptionsWindowViewModel>()
                .AddWindow<CertSelectWindow, CertSelectWindowViewModel>()
                .AddWindow<MainWindow, MainWindowViewModel>()
                .AddPage<CatalogPage, CatalogPageViewModel>(addPageAsSingleton: true)
                .AddPage<DetailPage, DetailPageViewModel>()
                .AddPage<QuickStartPage, QuickStartPageViewModel>()
                .AddWindow<SplashScreen, SplashScreenViewModel>()
                .AddSingleton<Application>(sp => new TableClothApplication(sp.GetRequiredService<IHost>()));

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

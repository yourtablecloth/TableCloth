using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Serilog;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using TableCloth.Commands;
using TableCloth.Commands.DisclaimerWindow;
using TableCloth.Commands.InputPasswordWindow;
using TableCloth.Commands.MainWindow;
using TableCloth.Commands.Shared;
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
            return 1; // 라이선스 미동의 시 종료
        }

        // 설치 후 첫 실행 시 파일 연결 등록
        RegisterFileAssociationsIfNeeded();
    
        return RunApp(args);
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
        catch
        {
            // 오류 발생 시 라이선스 동의 창 표시
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
        catch
        {
            // 저장 실패는 무시 - 다음 실행 시 다시 동의 요청
        }
    }

    private static string GetPreferencesFilePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataPath, "TableCloth", "Preferences.json");
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
        catch
        {
            // 레지스트리 등록 실패는 무시
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int RunApp(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Application.Current 속성은 아래 생성자를 호출하면서 자동으로 설정됩니다.
            var app = new App();

            app.SetupHost(CreateHostBuilder(args).Build());
            app.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex?.ToString() ?? "Unknown Error",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Environment.ExitCode;
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.ExceptionObject?.ToString() ?? "Unknown Error",
            "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static IHostBuilder CreateHostBuilder(
        string[]? args = default,
        Action<IConfigurationBuilder>? configurationBuilderOverride = default,
        Action<ILoggingBuilder>? loggingBuilderOverride = default,
        Action<IServiceCollection>? servicesBuilderOverride = default)
    {
        args ??= Helpers.GetCommandLineArguments();

        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(ConfigureAppConfiguration + configurationBuilderOverride)
            .ConfigureLogging(ConfigureLogging + loggingBuilderOverride)
            .ConfigureServices(ConfigureServices + servicesBuilderOverride);
    }

    private static void ConfigureAppConfiguration(IConfigurationBuilder configure)
    {
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging
            .AddSerilog(dispose: true)
            .AddConsole();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add Logging
        services.AddLogging();

        // Add HTTP Service
        services.AddHttpClient(
            nameof(ConstantStrings.UserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText));
        services.AddHttpClient(
            nameof(ConstantStrings.FamiliarUserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.FamiliarUserAgentText));
        services.AddHttpClient(
            nameof(StringResources.TableCloth_GitHubRestUAString),
            c => c.DefaultRequestHeaders.Add("User-Agent", StringResources.TableCloth_GitHubRestUAString));

        // Add Components
        services
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
            .AddSingleton<IShortcutCrerator, ShortcutCrerator>()
            .AddSingleton<ICommandLineArguments, CommandLineArguments>()
            .AddSingleton<IApplicationService, ApplicationService>()
            .AddSingleton<IArchiveExpander, ArchiveExpander>()
            .AddSingleton<ICatalogDeserializer, CatalogDeserializer>();

        // Shared Commands
        services
            .AddSingleton<LaunchSandboxCommand>()
            .AddSingleton<CreateShortcutCommand>()
            .AddSingleton<CertSelectCommand>()
            .AddSingleton<AppRestartCommand>()
            .AddSingleton<CopyCommandLineCommand>()
            .AddSingleton<AboutThisAppCommand>()
            .AddSingleton<ShowDebugInfoCommand>();

        // Disclaimer Window
        services
            .AddWindow<DisclaimerWindow, DisclaimerWindowViewModel>()
            .AddSingleton<DisclaimerWindowLoadedCommand>()
            .AddSingleton<DisclaimerWindowAcknowledgeCommand>();

        // Input Password Window
        services
            .AddWindow<InputPasswordWindow, InputPasswordWindowViewModel>()
            .AddSingleton<InputPasswordWindowLoadedCommand>()
            .AddSingleton<InputPasswordWindowConfirmCommand>()
            .AddSingleton<InputPasswordWindowCancelCommand>();

        // About Window
        services.AddWindow<AboutWindow, AboutWindowViewModel>();

        // Cert Select Window
        services.AddWindow<CertSelectWindow, CertSelectWindowViewModel>();

        // Main Window v2
        services
            .AddWindow<MainWindow, MainWindowViewModel>()
            .AddSingleton<MainWindowLoadedCommand>()
            .AddSingleton<MainWindowClosedCommand>();

        // Catalog Page
        services.AddPage<CatalogPage, CatalogPageViewModel>(addPageAsSingleton: true);

        // Detail Page
        services.AddPage<DetailPage, DetailPageViewModel>();

        // Splash Screen
        services.AddWindow<SplashScreen, SplashScreenViewModel>();

        // App
        services.AddTransient(_ => Application.Current);
    }
}

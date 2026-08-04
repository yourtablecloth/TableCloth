using AsyncAwaitBestPractices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using TableCloth.Components;
using TableCloth.Events;

namespace TableCloth;

public partial class TableClothApplication : Application
{
    public TableClothApplication()
    {
        InitializeComponent();
    }

    [ActivatorUtilitiesConstructor]
    public TableClothApplication(IHost host)
        : this()
    {
        Host = host.EnsureArgumentNotNull("Host initialization not done.", nameof(host));

        const string key = nameof(IServiceProvider);

        if (Properties.Contains(key) && Properties[key] != null)
            TableClothAppException.Throw("Already service provider has been initialized.");

        this.InitServiceProvider(host.Services);

        SafeFireAndForgetExtensions.Initialize();
        SafeFireAndForgetExtensions.SetDefaultExceptionHandling((thrownException) =>
        {
            var logger = host.Services.GetRequiredService<ILogger<TableClothApplication>>();
            logger.LogError(thrownException, "Unexpected error occurred.");

            if (Helpers.IsDevelopmentBuild)
            {
                var appMessageBox = host.Services.GetRequiredService<IAppMessageBox>();
                appMessageBox.DisplayError(thrownException, false);
            }
        });
    }

    public IHost? Host { get; private set; }

    private SplashScreen? _splashScreen;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var host = Host;
        ArgumentNullException.ThrowIfNull(host);

        var appUserInterface = host.Services.GetRequiredService<IAppUserInterface>();

        _splashScreen = appUserInterface.CreateSplashScreen();
        _splashScreen.ViewModel.InitializeDone += ViewModel_InitializeDone;
        _splashScreen.Show();
    }

    private async void ViewModel_InitializeDone(object? sender, DialogRequestEventArgs e)
    {
        var host = Host;
        ArgumentNullException.ThrowIfNull(host);

        _splashScreen = _splashScreen.EnsureNotNull("App initialization not done.");
        ArgumentNullException.ThrowIfNull(_splashScreen);

        if (!e.DialogResult.HasValue || !e.DialogResult.Value)
        {
            _splashScreen.Hide();
            _splashScreen.Close();
            return;
        }

        // 딥링크(`tablecloth:`)로 시작된 경우엔 스플래시를 닫지 않는다. 그 자리에서 샌드박스를 띄우고,
        // 인식된 사이트 이름과 함께 [닫기 / 다시 시도 / 식탁보 열기] 중 하나를 고르게 한다.
        // 인식하지 못했으면(대상 없음·게이트 탈락) 아래 평소 경로로 내려가 조용히 메인 창을 연다.
        var resolution = _splashScreen.ViewModel.ResolveStartupDeepLink();

        if (resolution.ShouldLaunchImmediately)
        {
            _splashScreen.ViewModel.OpenMainWindowRequested += (_, _) => ShowMainWindow(host);
            await _splashScreen.ViewModel.RunDeepLinkLaunchAsync(resolution);
            return;
        }

        _splashScreen.Hide();
        ShowMainWindow(host);
    }

    private MainWindow? _mainWindow;

    private void ShowMainWindow(IHost host)
    {
        // 딥링크 결과 화면에서 '식탁보 열기'로 들어오면 여러 번 눌릴 수 있다. 이미 열었으면 활성화만.
        //
        // 주의: 여기서 Application.MainWindow 로 판정하면 안 된다. WPF 는 처음 표시된 창을
        // MainWindow 로 자동 지정하므로, 스플래시가 먼저 뜬 시점에 이미 비어 있지 않다.
        // 그대로 두면 '식탁보 열기' 가 스플래시만 다시 활성화하고 끝난다.
        if (_mainWindow != null)
        {
            _mainWindow.Activate();
            return;
        }

        _mainWindow = host.Services.GetRequiredService<MainWindow>();
        MainWindow = _mainWindow;
        _mainWindow.Show();

        var splash = _splashScreen;
        _splashScreen = null;
        splash?.Close();
    }
}

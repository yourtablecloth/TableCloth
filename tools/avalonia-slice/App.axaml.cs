using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaSlice;

public partial class App : Application
{
    // Lemon.Hosting 이 DI 로 App 을 생성하며 이 생성자를 통해 서비스를 주입한다(vNext 선례).
    // 실 앱에서는 여기에 WindowManager/IServiceProvider 등을 주입해 창/VM 을 DI 로 만든다.
    [ActivatorUtilitiesConstructor]
    public App(MainViewModel mainViewModel) : this()
    {
        _mainViewModel = mainViewModel;
    }

    public App() { }

    private readonly MainViewModel? _mainViewModel;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            // DI 로 주입된 VM(서비스 주입됨)을 그대로 바인딩. 폴백은 파라미터리스 생성.
            desktop.MainWindow = new MainWindow { DataContext = _mainViewModel ?? new MainViewModel() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

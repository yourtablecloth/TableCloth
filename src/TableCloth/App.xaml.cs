using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Windows;
using TableCloth.Components;
using TableCloth.Events;

namespace TableCloth;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    internal void SetupHost(IHost host)
    {
        Host = host ?? throw new ArgumentException("Host initialization not done.", nameof(host));
        this.InitServiceProvider(host.Services);
    }

    public IHost? Host { get; private set; }

    private SplashScreen? _splashScreen;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        if (Host == null)
            throw new Exception("Host initialization not done.");

        var appUserInterface = Host.Services.GetRequiredService<IAppUserInterface>();
        _splashScreen = appUserInterface.CreateSplashScreen();
        _splashScreen.ViewModel.InitializeDone += ViewModel_InitializeDone;
        _splashScreen.Show();
    }

    private void ViewModel_InitializeDone(object? sender, DialogRequestEventArgs e)
    {
        if (Host == null)
            throw new Exception("App initialization not done.");

        if (_splashScreen == null)
            throw new Exception("App initialization not done.");

        _splashScreen.Hide();

        if (e.DialogResult.HasValue && e.DialogResult.Value)
        {
            var mainWindow = default(Window);
            if (_splashScreen.ViewModel.V2UIOptedIn)
                mainWindow = Host.Services.GetRequiredService<MainWindowV2>();
            else
                mainWindow = Host.Services.GetRequiredService<MainWindow>();

            this.MainWindow = mainWindow;
            mainWindow.Show();
        }

        _splashScreen.Close();
    }
}

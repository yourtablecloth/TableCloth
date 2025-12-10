using AsyncAwaitBestPractices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        Host = host.EnsureArgumentNotNull("Host initialization not done.", nameof(host));

        const string key = nameof(IServiceProvider);

        if (Properties.Contains(key) && Properties[key] != null)
            TableClothAppException.Throw("Already service provider has been initialized.");

        this.InitServiceProvider(host.Services);

        SafeFireAndForgetExtensions.Initialize();
        SafeFireAndForgetExtensions.SetDefaultExceptionHandling((thrownException) =>
        {
            var logger = host.Services.GetRequiredService<ILogger<App>>();
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

    private void ViewModel_InitializeDone(object? sender, DialogRequestEventArgs e)
    {
        var host = Host;
        ArgumentNullException.ThrowIfNull(host);

        _splashScreen = _splashScreen.EnsureNotNull("App initialization not done.");
        ArgumentNullException.ThrowIfNull(_splashScreen);

        _splashScreen.Hide();

        if (e.DialogResult.HasValue && e.DialogResult.Value)
        {
            MainWindow = host.Services.GetRequiredService<MainWindow>(); ;
            MainWindow.Show();
        }

        _splashScreen.Close();
    }
}

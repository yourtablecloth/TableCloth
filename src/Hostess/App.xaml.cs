using Hostess.Commands;
using Hostess.Commands.AboutWindow;
using Hostess.Commands.MainWindow;
using Hostess.Commands.PrecautionsWindow;
using Hostess.Components;
using Hostess.Dialogs;
using Hostess.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Hostess
{
    public partial class App : Application
    {
        public App()
        {
            Application.Current.InitServiceProvider(_serviceProvider = ConfigureServices());
            _appMessageBox = _serviceProvider.GetRequiredService<AppMessageBox>();
            _sharedProperties = _serviceProvider.GetRequiredService<SharedProperties>();
            _appUserInterface = _serviceProvider.GetRequiredService<AppUserInterface>();

            InitializeComponent();
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly AppMessageBox _appMessageBox;
        private readonly SharedProperties _sharedProperties;
        private readonly AppUserInterface _appUserInterface;
        
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;

            const int retryCount = 3;

            try
            {
                for (int attemptCount = 1; attemptCount <= retryCount; attemptCount++)
                {
                    CatalogDocument catalog = null;
                    string lastModifiedValue = null;

                    try
                    {
                        using (var webClient = new WebClient())
                        using (var catalogStream = webClient.OpenRead(StringResources.CatalogUrl))
                        {
                            lastModifiedValue = webClient.ResponseHeaders.Get("Last-Modified");
                            catalog = DeserializeFromXml<CatalogDocument>(catalogStream);

                            if (catalog == null)
                            {
                                throw new XmlException(StringResources.HostessError_CatalogDeserilizationFailure);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        catalog = null;
                        Thread.Sleep(TimeSpan.FromSeconds(1.5d * attemptCount));

                        if (attemptCount == retryCount)
                        {
                            _appMessageBox.DisplayError(StringResources.HostessError_CatalogLoadFailure(ex), true);
                            Application.Current.Shutdown(0);
                            return;
                        }

                        continue;
                    }

                    _sharedProperties.InitCatalogDocument(catalog);
                    _sharedProperties.InitCatalogLastModified(lastModifiedValue);
                    break;
                }

                var targetSites = e.Args.Where(x => !x.StartsWith(StringResources.TableCloth_Switch_Prefix, StringComparison.Ordinal)).ToArray();
                _sharedProperties.InitInstallSites(targetSites);

                var installEveryonesPrinter = false;
                var installAdobeReader = false;
                var installHancomOfficeViewer = false;
                var installRaiDrive = true;
                var hasIEModeEnabled = false;
                var showHelp = false;
                var hasDryRunEnabled = false;

                var options = e.Args.Where(x => x.StartsWith(StringResources.TableCloth_Switch_Prefix, StringComparison.Ordinal)).ToArray();
                foreach (var eachOption in options)
                {
                    if (eachOption.StartsWith(StringResources.TableCloth_Switch_InstallEveryonesPrinter, StringComparison.Ordinal))
                        installEveryonesPrinter = true;

                    if (eachOption.StartsWith(StringResources.TableCloth_Switch_InstallAdobeReader, StringComparison.Ordinal))
                        installAdobeReader = true;

                    if (eachOption.StartsWith(StringResources.TableCloth_Switch_InstallHancomOfficeViewer, StringComparison.Ordinal))
                        installHancomOfficeViewer = true;

                    if (eachOption.StartsWith(StringResources.TableCloth_Switch_InstallRaiDrive, StringComparison.Ordinal))
                        installRaiDrive = true;

                    if (eachOption.StartsWith(StringResources.TableCloth_Switch_EnableIEMode, StringComparison.Ordinal))
                        hasIEModeEnabled = true;

                    if (eachOption.StartsWith(StringResources.TableCloth_Switch_Help, StringComparison.Ordinal))
                        showHelp = true;

                    if (eachOption.StartsWith(StringResources.TableCloth_Switch_DryRun, StringComparison.Ordinal))
                        hasDryRunEnabled = true;
                }

                if (showHelp)
                {
                    _appMessageBox.DisplayInfo(StringResources.TableCloth_Hostess_Switches_Help);
                    Application.Current.Shutdown(0);
                    return;
                }

                _sharedProperties.InitWillInstallEveryonesPrinter(installEveryonesPrinter);
                _sharedProperties.InitWillInstallAdobeReader(installAdobeReader);
                _sharedProperties.InitWillInstallHancomOfficeViewer(installHancomOfficeViewer);
                _sharedProperties.InitWillInstallRaiDrive(installRaiDrive);
                _sharedProperties.InitHasIEModeEnabled(hasIEModeEnabled);
                _sharedProperties.InitHasDryRunEnabled(hasDryRunEnabled);

                if (!targetSites.Any())
                {
                    _appMessageBox.DisplayInfo(StringResources.Hostess_No_Targets);

                    Process.Start(new ProcessStartInfo("https://www.naver.com/")
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Maximized,
                    });

                    Application.Current.Shutdown(0);
                    return;
                }

                var mainWindow = _appUserInterface.CreateMainWindow();
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, true);
            }
        }

        private static T DeserializeFromXml<T>(Stream readableStream)
            where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            var xmlReaderSetting = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
            };

            using (var contentStream = XmlReader.Create(readableStream, xmlReaderSetting))
            {
                return (T)serializer.Deserialize(contentStream);
            }
        }

        private bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
            // If the certificate is a valid, signed certificate, return true.
            if (error == SslPolicyErrors.None)
                return true;

            _appMessageBox.DisplayError(StringResources.HostessError_X509CertError(cert, error), false);
            return false;
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add HTTP Service
            services.AddHttpClient(nameof(Hostess), c => c.DefaultRequestHeaders.Add("User-Agent", StringResources.UserAgentText));

            // Components
            services
                .AddSingleton<AppMessageBox>()
                .AddSingleton<AppUserInterface>()
                .AddSingleton<LicenseDescriptor>()
                .AddSingleton<ProtectTermService>()
                .AddSingleton<SharedProperties>()
                .AddSingleton<VisualThemeManager>()
                .AddSingleton<SharedLocations>();

            // Shared Commands
            services
                .AddSingleton<OpenAppHomepageCommand>()
                .AddSingleton<AboutThisAppCommand>();

            // About Window
            services
                .AddWindow<AboutWindow, AboutWindowViewModel>()
                .AddSingleton<AboutWindowLoadedCommand>()
                .AddSingleton<AboutWindowCloseCommand>();

            // Precautions Window
            services
                .AddWindow<PrecautionsWindow, PrecautionsWindowViewModel>()
                .AddSingleton<PrecautionsWindowLoadedCommand>()
                .AddSingleton<PrecautionsWindowCloseCommand>();

            // Main Window
            services
                .AddWindow<MainWindow, MainWindowViewModel>()
                .AddSingleton<MainWindowLoadedCommand>()
                .AddSingleton<MainWindowInstallPackagesCommand>();

            return services.BuildServiceProvider();
        }
    }
}

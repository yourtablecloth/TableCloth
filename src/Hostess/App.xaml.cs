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
            InitializeComponent();

            Services = ConfigureServices();
        }

        public static new App Current => (App)Application.Current;

        public IServiceProvider Services { get; }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var services = App.Current.Services;
            var appMessageBox = services.GetService<AppMessageBox>();
            var sharedProperties = services.GetService<SharedProperties>();

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
                            appMessageBox.DisplayError(StringResources.HostessError_CatalogLoadFailure(ex), true);
                            Current.Shutdown(0);
                            return;
                        }

                        continue;
                    }

                    sharedProperties.InitCatalogDocument(catalog);
                    sharedProperties.InitCatalogLastModified(lastModifiedValue);
                    break;
                }

                var targetSites = e.Args.Where(x => !x.StartsWith(StringResources.TableCloth_Switch_Prefix, StringComparison.Ordinal)).ToArray();
                sharedProperties.InitInstallSites(targetSites);

                var installEveryonesPrinter = false;
                var installAdobeReader = false;
                var installHancomOfficeViewer = false;
                var installRaiDrive = true;
                var hasIEModeEnabled = false;
                var showHelp = false;

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
                }

                if (showHelp)
                {
                    appMessageBox.DisplayInfo(StringResources.TableCloth_Hostess_Switches_Help);
                    Current.Shutdown(0);
                    return;
                }

                sharedProperties.InitWillInstallEveryonesPrinter(installEveryonesPrinter);
                sharedProperties.InitWillInstallAdobeReader(installAdobeReader);
                sharedProperties.InitWillInstallHancomOfficeViewer(installHancomOfficeViewer);
                sharedProperties.InitWillInstallRaiDrive(installRaiDrive);
                sharedProperties.InitHasIEModeEnabled(hasIEModeEnabled);

                if (!targetSites.Any())
                {
                    appMessageBox.DisplayInfo(StringResources.Hostess_No_Targets);

                    Process.Start(new ProcessStartInfo("https://www.naver.com/")
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Maximized,
                    });

                    Current.Shutdown(0);
                    return;
                }
            }
            catch (Exception ex)
            {
                appMessageBox.DisplayError(ex, true);
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
            var services = App.Current.Services;
            var appMessageBox = services.GetService<AppMessageBox>();

            // If the certificate is a valid, signed certificate, return true.
            if (error == SslPolicyErrors.None)
                return true;

            appMessageBox.DisplayError(StringResources.HostessError_X509CertError(cert, error), false);
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
                .AddSingleton<VisualThemeManager>();

            // Shared Commands
            services
                .AddSingleton<OpenAppHomepageCommand>()
                .AddSingleton<ShowErrorMessageCommand>()
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

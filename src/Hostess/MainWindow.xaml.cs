using Hostess.Components;
using Hostess.Dialogs;
using Hostess.Themes;
using Hostess.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Interop;
using TableCloth;
using TableCloth.Resources;

namespace Hostess
{
    public partial class MainWindow : Window
    {
        public MainWindow()
            => InitializeComponent();

        private readonly string[] validAccountNames = new string[]
        {
            "ContainerAdministrator",
            "ContainerUser",
            "WDAGUtilityAccount",
        };

        private void SetDesktopWallpaper()
        {
            var picturesDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

            if (!Directory.Exists(picturesDirectoryPath))
                Directory.CreateDirectory(picturesDirectoryPath);

            var wallpaperPath = Path.Combine(picturesDirectoryPath, "Signature.jpg");
            Properties.Resources.Signature.Save(wallpaperPath, ImageFormat.Jpeg);

            _ = NativeMethods.SystemParametersInfoW(
                NativeMethods.SetDesktopWallpaper, 0, wallpaperPath,
                NativeMethods.UpdateIniFile | NativeMethods.SendWinIniChange);

            NativeMethods.UpdatePerUserSystemParameters(
                IntPtr.Zero, IntPtr.Zero, "1, True", 0);
        }

        private void VerifyWindowsContainerEnvironment()
        {
            if (!validAccountNames.Contains(Environment.UserName, StringComparer.Ordinal))
            {
                var message = (string)Application.Current.Resources["WarningForNonSandboxEnvironment"];
                var title = (string)Application.Current.Resources["ErrorDialogTitle"];
                var response = MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);

                if (response != MessageBoxResult.Yes)
                    Environment.Exit(1);
            }
        }

        private bool? IsLightThemeApplied()
        {
            // https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
            using (var personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            {
                if (personalizeKey != null)
                {
                    if (personalizeKey.GetValueKind("AppsUseLightTheme") == RegistryValueKind.DWord)
                    {
                        return (int)personalizeKey.GetValue("AppsUseLightTheme", 1) > 0;
                    }
                }
            }

            return null;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
            const int WM_SETTINGCHANGE = 0x001A;

            if (msg == WM_SETTINGCHANGE)
            {
                var data = Marshal.PtrToStringAuto(lParam);
                if (string.Equals(data, "ImmersiveColorSet", StringComparison.Ordinal))
                {
                    var appliedLightTheme = IsLightThemeApplied();
                    if (appliedLightTheme.HasValue)
                    {
                        if (appliedLightTheme.Value)
                            ThemesController.CurrentTheme = ThemeTypes.ColourfulLight;
                        else
                            ThemesController.CurrentTheme = ThemeTypes.ColourfulDark;
                        handled = true;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var services = App.Current.Services;
            var protectTermService = services.GetRequiredService<ProtectTermService>();
            var sharedProperties = services.GetRequiredService<SharedProperties>();

            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);

            var appliedLightTheme = IsLightThemeApplied();
            if (appliedLightTheme.HasValue)
            {
                if (appliedLightTheme.Value)
                    ThemesController.CurrentTheme = ThemeTypes.ColourfulLight;
                else
                    ThemesController.CurrentTheme = ThemeTypes.ColourfulDark;
            }

            Width = MinWidth;
            Height = SystemParameters.PrimaryScreenHeight * 0.5;
            Top = (SystemParameters.PrimaryScreenHeight / 2) - (Height / 2);
            Left = SystemParameters.PrimaryScreenWidth - Width;

            VerifyWindowsContainerEnvironment();

            try { protectTermService.PreventServiceProcessTermination("TermService"); }
            catch (AggregateException aex)
            {
                MessageBox.Show(this, $"{aex.InnerException.Message}", StringResources.TitleText_Error,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"{ex.Message}", StringResources.TitleText_Error,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }

            try { protectTermService.PreventServiceStop("TermService", Environment.UserName); }
            catch (AggregateException aex)
            {
                MessageBox.Show(this, $"{aex.InnerException.Message}", StringResources.TitleText_Error,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"{ex.Message}", StringResources.TitleText_Error,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
            }

            SetDesktopWallpaper();

            var catalog = sharedProperties.GetCatalogDocument();
            var targets = sharedProperties.GetInstallSites();
            var packages = new List<InstallItemViewModel>();

            foreach (var eachTargetName in targets)
            {
                var targetService = catalog.Services.FirstOrDefault(x => string.Equals(eachTargetName, x.Id, StringComparison.Ordinal));

                if (targetService == null)
                {
                    continue;
                }

                packages.AddRange(targetService.Packages.Select(eachPackage => new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.DownloadAndInstall,
                    TargetSiteName = targetService.DisplayName,
                    TargetSiteUrl = targetService.Url,
                    PackageName = eachPackage.Name,
                    PackageUrl = eachPackage.Url,
                    Arguments = eachPackage.Arguments,
                    Installed = null,
                }));

                var bootstrapData = targetService.CustomBootstrap;

                if (!string.IsNullOrWhiteSpace(bootstrapData))
                {
                    packages.Add(new InstallItemViewModel()
                    {
                        InstallItemType = InstallItemType.PowerShellScript,
                        TargetSiteName = targetService.DisplayName,
                        TargetSiteUrl = targetService.Url,
                        PackageName = StringResources.Hostess_CustomScript_Title,
                        ScriptContent = bootstrapData,
                    });
                }
            }

            InstallList.ItemsSource = new ObservableCollection<InstallItemViewModel>(packages);

            if (catalog.Services.Where(x => targets.Contains(x.Id)).Any(x => !string.IsNullOrWhiteSpace(x.CompatibilityNotes?.Trim())))
            {
                var window = new PrecautionsWindow();
                window.ShowDialog();
            }
            else
            {
                var peer = new ButtonAutomationPeer(PerformInstallButton);
                var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var services = App.Current.Services;
            var sharedProperties = services.GetRequiredService<SharedProperties>();
            var licenseDescriptor = services.GetRequiredService<LicenseDescriptor>();

            var aboutWindow = new AboutWindow()
            {
                CatalogDate = sharedProperties.GetCatalogLastModified(),
                License = licenseDescriptor.GetLicenseDescriptions(),
            };

            aboutWindow.ShowDialog();
        }

        private async void PerformInstallButton_Click(object sender, RoutedEventArgs e)
        {
            var services = App.Current.Services;
            var sharedProperties = services.GetRequiredService<SharedProperties>();

            try
            {
                PerformInstallButton.IsEnabled = false;
                var hasAnyFailure = false;

                var downloadFolderPath = NativeMethods.GetKnownFolderPath(NativeMethods.DownloadFolderGuid);

                if (!Directory.Exists(downloadFolderPath))
                    Directory.CreateDirectory(downloadFolderPath);

                var catalog = sharedProperties.GetCatalogDocument();

                if (sharedProperties.GetHasIEModeEnabled())
                {
                    try
                    {
                        // HKLM\SOFTWARE\Policies\Microsoft\Edge > InternetExplorerIntegrationLevel (REG_DWORD) with value 1, InternetExplorerIntegrationSiteList (REG_SZ)
                        using (var ieModeKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Edge", true))
                        {
                            ieModeKey.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                            ieModeKey.SetValue("InternetExplorerIntegrationSiteList", StringResources.IEModePolicyXmlUrl, RegistryValueKind.String);
                        }

                        // msedge.exe 파일 경로를 유추하고, Policy를 반영하기 위해 잠시 실행했다가 종료하는 동작을 추가
                        var msedgeKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe", false);
                        var msedgePath = default(string);

                        if (msedgeKey != null)
                        {
                            using (msedgeKey)
                            {
                                msedgePath = (string)msedgeKey.GetValue(null, null);
                            }
                        }

                        if (!File.Exists(msedgePath))
                        {
                            msedgePath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                                "Microsoft", "Edge", "Application", "msedge.exe");
                        }

                        if (File.Exists(msedgePath))
                        {
                            var msedgePsi = new ProcessStartInfo(msedgePath, "about:blank")
                            {
                                UseShellExecute = false,
                                WindowStyle = ProcessWindowStyle.Minimized,
                            };

                            using (var msedgeProcess = Process.Start(msedgePsi))
                            {
                                var tcs = new TaskCompletionSource<int>();
                                msedgeProcess.EnableRaisingEvents = true;
                                msedgeProcess.Exited += (_sender, _e) =>
                                {
                                    tcs.SetResult(msedgeProcess.ExitCode);
                                };
                                await Task.Delay(TimeSpan.FromSeconds(1.5d));
                                msedgeProcess.CloseMainWindow();
                                await tcs.Task;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }
                }

                foreach (InstallItemViewModel eachItem in InstallList.ItemsSource)
                {
                    try
                    {
                        if (eachItem.InstallItemType == InstallItemType.DownloadAndInstall)
                        {
                            eachItem.Installed = null;
                            eachItem.StatusMessage = StringResources.Hostess_Download_InProgress;

                            var tempFileName = $"installer_{Guid.NewGuid():n}.exe";
                            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

                            if (File.Exists(tempFilePath))
                                File.Delete(tempFilePath);

                            using (var webClient = new WebClient())
                            {
                                webClient.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml");
                                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Trident/7.0; rv:11.0) like Gecko");
                                await webClient.DownloadFileTaskAsync(eachItem.PackageUrl, tempFilePath);

                                eachItem.StatusMessage = StringResources.Hostess_Install_InProgress;
                                var psi = new ProcessStartInfo(tempFilePath, eachItem.Arguments)
                                {
                                    UseShellExecute = false,
                                };

                                var cpSource = new TaskCompletionSource<int>();
                                using (var process = new Process() { StartInfo = psi, })
                                {
                                    process.EnableRaisingEvents = true;
                                    process.Exited += (_sender, _e) =>
                                    {
                                        var realSender = _sender as Process;
                                        cpSource.SetResult(realSender.ExitCode);
                                    };

                                    if (!process.Start())
                                        throw new ApplicationException(StringResources.HostessError_Package_CanNotStart);

                                    await cpSource.Task;
                                    eachItem.StatusMessage = StringResources.Hostess_Install_Succeed;
                                    eachItem.Installed = true;
                                    eachItem.ErrorMessage = null;
                                }
                            }
                        }
                        else if (eachItem.InstallItemType == InstallItemType.PowerShellScript)
                        {
                            eachItem.Installed = null;
                            eachItem.StatusMessage = StringResources.Hostess_Install_InProgress;

                            var tempFileName = $"bootstrap_{Guid.NewGuid():n}.ps1";
                            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

                            if (File.Exists(tempFilePath))
                                File.Delete(tempFilePath);

                            File.WriteAllText(tempFilePath, eachItem.ScriptContent, Encoding.Unicode);
                            var powershellPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.System),
                                @"WindowsPowerShell\v1.0\powershell.exe");

                            if (!File.Exists(powershellPath))
                                throw new Exception(StringResources.Hostess_No_PowerShell_Error);

                            var psi = new ProcessStartInfo(powershellPath, $"Set-ExecutionPolicy Bypass -Scope Process -Force; {tempFilePath}")
                            {
                                UseShellExecute = false,
                            };

                            var cpSource = new TaskCompletionSource<int>();
                            using (var process = new Process() { StartInfo = psi, })
                            {
                                process.EnableRaisingEvents = true;
                                process.Exited += (_sender, _e) =>
                                {
                                    var realSender = _sender as Process;
                                    cpSource.SetResult(realSender.ExitCode);
                                };

                                if (!process.Start())
                                    throw new ApplicationException(StringResources.HostessError_Package_CanNotStart);

                                await cpSource.Task;
                                eachItem.StatusMessage = StringResources.Hostess_Install_Succeed;
                                eachItem.Installed = true;
                                eachItem.ErrorMessage = null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        hasAnyFailure = true;
                        eachItem.StatusMessage = StringResources.Hostess_Install_Failed;
                        eachItem.Installed = false;
                        eachItem.ErrorMessage = ex is AggregateException exception ? exception.InnerException.Message : ex.Message;
                        await Task.Delay(100);
                    }
                }

                if (!hasAnyFailure)
                {
                    if (sharedProperties.WillInstallEveryonesPrinter())
                    {
                        Process.Start(new ProcessStartInfo(StringResources.EveryonesPrinterUrl)
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Maximized,
                        });
                    }

                    if (sharedProperties.WillInstallAdobeReader())
                    {
                        Process.Start(new ProcessStartInfo(StringResources.AdobeReaderUrl)
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Maximized,
                        });
                    }

                    if (sharedProperties.WillInstallHancomOfficeViewer())
                    {
                        Process.Start(new ProcessStartInfo(StringResources.HancomOfficeViewerUrl)
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Maximized,
                        });
                    }

                    if (sharedProperties.WillInstallRaiDrive())
                    {
                        Process.Start(new ProcessStartInfo(StringResources.RaiDriveUrl)
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Maximized,
                        });
                    }

                    var targets = sharedProperties.GetInstallSites();

                    foreach (var eachUrl in catalog.Services.Where(x => targets.Contains(x.Id)).Select(x => x.Url))
                    {
                        Process.Start(new ProcessStartInfo(eachUrl)
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Maximized,
                        });
                    }

                    Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                PerformInstallButton.IsEnabled = true;
            }
        }
    }
}

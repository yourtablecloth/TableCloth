using Hostess.Components;
using Hostess.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;
using TableCloth.Resources;

namespace Hostess.Commands.MainWindow
{
    public sealed class MainWindowLoadedCommand : ViewModelCommandBase<MainWindowViewModel>
    {
        public MainWindowLoadedCommand(
            Application application,
            IResourceCacheManager resourceCacheManager,
            ICriticalServiceProtector criticalServiceProtector,
            IAppMessageBox appMessageBox,
            IAppUserInterface appUserInterface,
            IVisualThemeManager visualThemeManager,
            ISharedLocations sharedLocations,
            ICommandLineArguments commandLineArguments)
        {
            _application = application;
            _resourceCacheManager = resourceCacheManager;
            _criticalServiceProtector = criticalServiceProtector;
            _appMessageBox = appMessageBox;
            _appUserInterface = appUserInterface;
            _visualThemeManager = visualThemeManager;
            _sharedLocations = sharedLocations;
            _commandLineArguments = commandLineArguments;
        }

        private readonly Application _application;
        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly ICriticalServiceProtector _criticalServiceProtector;
        private readonly IAppMessageBox _appMessageBox;
        private readonly IAppUserInterface _appUserInterface;
        private readonly IVisualThemeManager _visualThemeManager;
        private readonly ISharedLocations _sharedLocations;
        private readonly ICommandLineArguments _commandLineArguments;

        private readonly string[] _validAccountNames = new string[]
        {
            "ContainerAdministrator",
            "ContainerUser",
            "WDAGUtilityAccount",
        };

        public override void Execute(MainWindowViewModel viewModel)
        {
            var parsedArgs = _commandLineArguments.Current;
            viewModel.ShowDryRunNotification = parsedArgs.DryRun;

            _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow);

            viewModel.NotifyWindowLoaded(this, EventArgs.Empty);

            var catalog = _resourceCacheManager.CatalogDocument;
            var targets = parsedArgs.SelectedServices;

            var packages = new List<InstallItemViewModel>
            {
                new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.CustomAction,
                    TargetSiteName = UIStringResources.Option_Prerequisites,
                    PackageName = UIStringResources.Install_VerifyEnvironment,
                    CustomAction = VerifyWindowsContainerEnvironment,
                },
                new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.CustomAction,
                    TargetSiteName = UIStringResources.Option_Prerequisites,
                    PackageName = UIStringResources.Install_PrepareEnvironment,
                    CustomAction = PrepareDirectoriesAsync,
                },
                new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.CustomAction,
                    TargetSiteName = UIStringResources.Option_Prerequisites,
                    PackageName = UIStringResources.Install_TryProtectCriticalServices,
                    CustomAction = TryProtectCriticalServices,
                },
                new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.CustomAction,
                    TargetSiteName = UIStringResources.Option_Prerequisites,
                    PackageName = UIStringResources.Install_SetDesktopWallpaper,
                    CustomAction = SetDesktopWallpaper,
                },
            };

            if (parsedArgs.EnableInternetExplorerMode.HasValue &&
                parsedArgs.EnableInternetExplorerMode.Value)
            {
                packages.Add(new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.CustomAction,
                    TargetSiteName = UIStringResources.Option_Prerequisites,
                    PackageName = UIStringResources.Install_EnableIEMode,
                    CustomAction = EnableIEModeAsync,
                });
            }


            foreach (var eachTargetName in targets)
            {
                var targetService = catalog.Services.FirstOrDefault(x => string.Equals(eachTargetName, x.Id, StringComparison.Ordinal));

                if (targetService == null)
                    continue;

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
                        PackageName = UIStringResources.Hostess_CustomScript_Title,
                        ScriptContent = bootstrapData,
                    });
                }
            }

            if (parsedArgs.InstallAdobeReader.HasValue &&
                parsedArgs.InstallAdobeReader.Value)
            {
                packages.Add(new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.OpenWebSite,
                    TargetSiteName = UIStringResources.Option_Addin,
                    TargetSiteUrl = CommonStrings.AppInfoUrl,
                    PackageName = UIStringResources.Option_InstallAdobeReader,
                    PackageUrl = CommonStrings.AdobeReaderUrl,
                    Arguments = string.Empty,
                    ScriptContent = string.Empty,
                });
            }

            if (parsedArgs.InstallEveryonesPrinter.HasValue &&
                parsedArgs.InstallEveryonesPrinter.Value)
            {
                packages.Add(new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.OpenWebSite,
                    TargetSiteName = UIStringResources.Option_Addin,
                    PackageName = UIStringResources.Option_InstallEveryonesPrinter,
                    PackageUrl = CommonStrings.EveryonesPrinterUrl,
                });
            }

            if (parsedArgs.InstallHancomOfficeViewer.HasValue &&
                parsedArgs.InstallHancomOfficeViewer.Value)
            {
                packages.Add(new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.OpenWebSite,
                    TargetSiteName = UIStringResources.Option_Addin,
                    PackageName = UIStringResources.Option_InstallHancomOfficeViewer,
                    PackageUrl = CommonStrings.HancomOfficeViewerUrl,
                });
            }

            if (parsedArgs.InstallRaiDrive.HasValue &&
                parsedArgs.InstallRaiDrive.Value)
            {
                packages.Add(new InstallItemViewModel()
                {
                    InstallItemType = InstallItemType.OpenWebSite,
                    TargetSiteName = UIStringResources.Option_Addin,
                    PackageName = UIStringResources.Option_InstallRaiDrive,
                    PackageUrl = CommonStrings.RaiDriveUrl,
                });
            }

            viewModel.InstallItems = new ObservableCollection<InstallItemViewModel>(packages);

            if (catalog.Services.Where(x => targets.Contains(x.Id)).Any(x => !string.IsNullOrWhiteSpace(x.CompatibilityNotes?.Trim())))
            {
                var window = _appUserInterface.CreatePrecautionsWindow();
                window.ShowDialog();
            }
            else
            {
                viewModel.MainWindowInstallPackagesCommand.Execute(viewModel);
            }
        }

        private async Task PrepareDirectoriesAsync(InstallItemViewModel viewModel)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                return;
            }

            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();

            if (!Directory.Exists(downloadFolderPath))
                Directory.CreateDirectory(downloadFolderPath);
        }

        private async Task EnableIEModeAsync(InstallItemViewModel viewModel)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                return;
            }

            if (parsedArgs.EnableInternetExplorerMode ?? false)
            {
                // HKLM\SOFTWARE\Policies\Microsoft\Edge > InternetExplorerIntegrationLevel (REG_DWORD) with value 1, InternetExplorerIntegrationSiteList (REG_SZ)
                using (var ieModeKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Edge", true))
                {
                    ieModeKey.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                    ieModeKey.SetValue("InternetExplorerIntegrationSiteList", ConstantStrings.IEModePolicyXmlUrl, RegistryValueKind.String);
                }

                // msedge.exe 파일 경로를 유추하고, Policy를 반영하기 위해 잠시 실행했다가 종료하는 동작을 추가
                if (!_sharedLocations.TryGetMicrosoftEdgeExecutableFilePath(out var msedgePath))
                    msedgePath = _sharedLocations.GetDefaultX86MicrosoftEdgeExecutableFilePath();

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
                        await Task.Delay(TimeSpan.FromSeconds(1.5d)).ConfigureAwait(false);
                        msedgeProcess.CloseMainWindow();
                        await tcs.Task.ConfigureAwait(false);
                    }
                }
            }
        }

        private async Task VerifyWindowsContainerEnvironment(InstallItemViewModel viewModel)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                return;
            }

            if (!_validAccountNames.Contains(Environment.UserName, StringComparer.Ordinal))
            {
                var response = _appMessageBox.DisplayQuestion(
                    AskStrings.Ask_WarningForNonSandboxEnvironment,
                    defaultAnswer: MessageBoxResult.No);

                if (response != MessageBoxResult.Yes)
                    Environment.Exit(1);
            }
        }

        private async Task TryProtectCriticalServices(InstallItemViewModel viewModel)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                return;
            }

            try { _criticalServiceProtector.PreventServiceProcessTermination("TermService"); }
            catch (AggregateException aex) { _appMessageBox.DisplayError(aex.InnerException, false); }
            catch (Exception ex) { _appMessageBox.DisplayError(ex, false); }

            try { _criticalServiceProtector.PreventServiceStop("TermService", Environment.UserName); }
            catch (AggregateException aex) { _appMessageBox.DisplayError(aex.InnerException, false); }
            catch (Exception ex) { _appMessageBox.DisplayError(ex, false); }
        }

        private async Task SetDesktopWallpaper(InstallItemViewModel viewModel)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                return;
            }

            var picturesDirectoryPath = _sharedLocations.GetPicturesDirectoryPath();

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
    }
}

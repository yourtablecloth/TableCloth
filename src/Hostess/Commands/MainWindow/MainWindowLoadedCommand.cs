using Hostess.Components;
using Hostess.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using TableCloth;
using TableCloth.Resources;

namespace Hostess.Commands.MainWindow
{
    public sealed class MainWindowLoadedCommand : ViewModelCommandBase<MainWindowViewModel>
    {
        public MainWindowLoadedCommand(
            ProtectCriticalServices protectTermServices,
            SharedProperties sharedProperties,
            AppMessageBox appMessageBox,
            AppUserInterface appUserInterface,
            VisualThemeManager visualThemeManager,
            SharedLocations sharedLocations)
        {
            _protectCriticalServices = protectTermServices;
            _sharedProperties = sharedProperties;
            _appMessageBox = appMessageBox;
            _appUserInterface = appUserInterface;
            _visualThemeManager = visualThemeManager;
            _sharedLocations = sharedLocations;
        }

        private readonly ProtectCriticalServices _protectCriticalServices;
        private readonly SharedProperties _sharedProperties;
        private readonly AppMessageBox _appMessageBox;
        private readonly AppUserInterface _appUserInterface;
        private readonly VisualThemeManager _visualThemeManager;
        private readonly SharedLocations _sharedLocations;

        private readonly string[] _validAccountNames = new string[]
        {
            "ContainerAdministrator",
            "ContainerUser",
            "WDAGUtilityAccount",
        };

        public override void Execute(MainWindowViewModel viewModel)
        {
            viewModel.ShowDryRunNotification = _sharedProperties.HasDryRunEnabled();

            _visualThemeManager.ApplyAutoThemeChange(
                Application.Current.MainWindow);

            viewModel.NotifyWindowLoaded(this, EventArgs.Empty);

            VerifyWindowsContainerEnvironment();
            TryProtectCriticalServices();
            SetDesktopWallpaper();

            var catalog = _sharedProperties.GetCatalogDocument();
            var targets = _sharedProperties.GetInstallSites();
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

        private void VerifyWindowsContainerEnvironment()
        {
            if (_sharedProperties.HasDryRunEnabled())
                return;

            if (!_validAccountNames.Contains(Environment.UserName, StringComparer.Ordinal))
            {
                var response = _appMessageBox.DisplayQuestion(
                    (string)Application.Current.Resources["WarningForNonSandboxEnvironment"],
                    defaultAnswer: MessageBoxResult.No);

                if (response != MessageBoxResult.Yes)
                    Environment.Exit(1);
            }
        }

        private void TryProtectCriticalServices()
        {
            if (_sharedProperties.HasDryRunEnabled())
                return;

            try { _protectCriticalServices.PreventServiceProcessTermination("TermService"); }
            catch (AggregateException aex) { _appMessageBox.DisplayError(aex.InnerException, false); }
            catch (Exception ex) { _appMessageBox.DisplayError(ex, false); }

            try { _protectCriticalServices.PreventServiceStop("TermService", Environment.UserName); }
            catch (AggregateException aex) { _appMessageBox.DisplayError(aex.InnerException, false); }
            catch (Exception ex) { _appMessageBox.DisplayError(ex, false); }
        }

        private void SetDesktopWallpaper()
        {
            if (_sharedProperties.HasDryRunEnabled())
                return;

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

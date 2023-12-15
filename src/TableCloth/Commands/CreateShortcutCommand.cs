using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands
{
    public sealed class CreateShortcutCommand : BaseCommand
    {
        public CreateShortcutCommand(
            CommandLineComposer commandLineComposer,
            SharedLocations sharedLocations,
            AppMessageBox appMessageBox)
        {
            this.commandLineComposer = commandLineComposer;
            this.sharedLocations = sharedLocations;
            this.appMessageBox = appMessageBox;
        }

        private readonly CommandLineComposer commandLineComposer;
        private readonly SharedLocations sharedLocations;
        private readonly AppMessageBox appMessageBox;

        public override void Execute(object parameter)
        {
            switch (parameter)
            {
                case MainWindowViewModel v1ViewModel:
                    this.ExecuteFromV1Model(v1ViewModel);
                    break;

                case DetailPageViewModel v2ViewModel:
                    this.ExecuteFromV2Model(v2ViewModel);
                    break;

                default:
                    throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));
            }
        }

        private void ExecuteFromV1Model(MainWindowViewModel viewModel)
        {
            var options = new List<string>();
            var targetPath = Process.GetCurrentProcess().MainModule.FileName;
            var linkName = StringResources.AppName;

            if (viewModel.EnableMicrophone)
                options.Add(StringResources.TableCloth_Switch_EnableMicrophone);
            if (viewModel.EnableWebCam)
                options.Add(StringResources.TableCloth_Switch_EnableCamera);
            if (viewModel.EnablePrinters)
                options.Add(StringResources.TableCloth_Switch_EnablePrinter);
            if (viewModel.InstallEveryonesPrinter)
                options.Add(StringResources.TableCloth_Switch_InstallEveryonesPrinter);
            if (viewModel.InstallAdobeReader)
                options.Add(StringResources.TableCloth_Switch_InstallAdobeReader);
            if (viewModel.InstallHancomOfficeViewer)
                options.Add(StringResources.TableCloth_Switch_InstallHancomOfficeViewer);
            if (viewModel.InstallRaiDrive)
                options.Add(StringResources.TableCloth_Switch_InstallRaiDrive);
            if (viewModel.EnableInternetExplorerMode)
                options.Add(StringResources.TableCloth_Switch_EnableIEMode);
            if (viewModel.MapNpkiCert)
                options.Add(StringResources.Tablecloth_Switch_EnableCert);

            // 단축 아이콘은 지정 가능한 명령줄의 길이가 260자가 최대인 관계로 여러 사이트를 지정하는 것이 어려움.
            var firstSite = viewModel.SelectedServices?.FirstOrDefault();
            var iconFilePath = default(string);

            if (firstSite != null)
            {
                options.Add(firstSite.Id);
                linkName = firstSite.DisplayName;

                iconFilePath = Path.Combine(
                    this.sharedLocations.GetImageDirectoryPath(),
                    $"{firstSite.Id}.ico");

                if (!File.Exists(iconFilePath))
                    iconFilePath = null;
            }

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fullPath = Path.Combine(desktopPath, linkName + ".lnk");

            for (int i = 1; File.Exists(fullPath); ++i)
                fullPath = Path.Combine(desktopPath, linkName + $" ({i}).lnk");

            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(fullPath);
                shortcut.TargetPath = targetPath;

                if (iconFilePath != null && File.Exists(iconFilePath))
                    shortcut.IconLocation = iconFilePath;

                shortcut.Arguments = String.Join(' ', options.ToArray());
                shortcut.Save();
            }
            catch
            {
                this.appMessageBox.DisplayInfo(StringResources.Error_ShortcutFailed);
                return;
            }

            this.appMessageBox.DisplayInfo(StringResources.Info_ShortcutSuccess);
        }

        private void ExecuteFromV2Model(DetailPageViewModel viewModel)
        {
            var targetPath = this.sharedLocations.ExecutableFilePath;
            var linkName = StringResources.AppName;

            var firstSite = viewModel.SelectedService;
            var iconFilePath = default(string);

            if (firstSite != null)
            {
                linkName = firstSite.DisplayName;

                iconFilePath = Path.Combine(
                    this.sharedLocations.GetImageDirectoryPath(),
                    $"{firstSite.Id}.ico");

                if (!File.Exists(iconFilePath))
                    iconFilePath = null;
            }

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fullPath = Path.Combine(desktopPath, linkName + ".lnk");

            for (int i = 1; File.Exists(fullPath); ++i)
                fullPath = Path.Combine(desktopPath, linkName + $" ({i}).lnk");

            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(fullPath);
                shortcut.TargetPath = targetPath;

                if (iconFilePath != null && File.Exists(iconFilePath))
                    shortcut.IconLocation = iconFilePath;

                shortcut.Arguments = this.commandLineComposer.ComposeCommandLineArguments(viewModel);
                shortcut.Save();
            }
            catch
            {
                this.appMessageBox.DisplayInfo(StringResources.Error_ShortcutFailed);
                return;
            }

            this.appMessageBox.DisplayInfo(StringResources.Info_ShortcutSuccess);
        }
    }
}

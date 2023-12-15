using System;
using System.Collections.Generic;
using System.IO;
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
            if (parameter is not DetailPageViewModel viewModel)
                throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

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

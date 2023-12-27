using System;
using System.IO;
using System.Linq;
using TableCloth.Contracts;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Components
{
    public sealed class ShortcutCrerator
    {
        public ShortcutCrerator(
            CommandLineComposer commandLineComposer,
            SharedLocations sharedLocations,
            AppMessageBox appMessageBox)
        {
            _commandLineComposer = commandLineComposer;
            _sharedLocations = sharedLocations;
            _appMessageBox = appMessageBox;
        }

        private readonly CommandLineComposer _commandLineComposer;
        private readonly SharedLocations _sharedLocations;
        private readonly AppMessageBox _appMessageBox;

        public void CreateShortcut(ITableClothViewModel viewModel)
        {
            var targetPath = _sharedLocations.ExecutableFilePath;
            var linkName = StringResources.AppName;

            var firstSite = viewModel.SelectedServices.FirstOrDefault();
            var iconFilePath = default(string);

            if (firstSite != null)
            {
                linkName = firstSite.DisplayName;

                iconFilePath = Path.Combine(
                    _sharedLocations.GetImageDirectoryPath(),
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
                var shellType = Type.GetTypeFromProgID("WScript.Shell")
                    ?? throw new Exception("Cannot obtain WScript.Shell object type information.");
                var shell = Activator.CreateInstance(shellType)
                    ?? throw new Exception("Cannot obtain WScript.Shell object instance.");
                dynamic shortcut = ((dynamic)shell).CreateShortcut(fullPath);
                shortcut.TargetPath = targetPath;

                if (iconFilePath != null && File.Exists(iconFilePath))
                    shortcut.IconLocation = iconFilePath;

                shortcut.Arguments = _commandLineComposer.ComposeCommandLineArguments(viewModel, false);
                shortcut.Save();
            }
            catch
            {
                _appMessageBox.DisplayInfo(StringResources.Error_ShortcutFailed);
                return;
            }

            _appMessageBox.DisplayInfo(StringResources.Info_ShortcutSuccess);
        }
    }
}

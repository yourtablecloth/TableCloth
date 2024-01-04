using System;
using System.IO;
using System.Linq;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public sealed class ShortcutCrerator(
    ICommandLineComposer commandLineComposer,
    ISharedLocations sharedLocations,
    IAppMessageBox appMessageBox) : IShortcutCrerator
{
    public void CreateShortcut(ITableClothViewModel viewModel)
    {
        var targetPath = sharedLocations.ExecutableFilePath;
        var linkName = StringResources.AppName;

        var firstSite = viewModel.SelectedServices.FirstOrDefault();
        var iconFilePath = default(string);

        if (firstSite != null)
        {
            linkName = firstSite.DisplayName;

            iconFilePath = Path.Combine(
                sharedLocations.GetImageDirectoryPath(),
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

            shortcut.Arguments = commandLineComposer.ComposeCommandLineArguments(viewModel, false);
            shortcut.Save();
        }
        catch
        {
            appMessageBox.DisplayInfo(StringResources.Error_ShortcutFailed);
            return;
        }

        appMessageBox.DisplayInfo(StringResources.Info_ShortcutSuccess);
    }
}

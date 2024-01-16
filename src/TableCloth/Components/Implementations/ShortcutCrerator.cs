using System;
using System.IO;
using System.Linq;
using TableCloth.Interop.WshRuntimeLibrary;
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
        var linkName = ConstantStrings.AppNameForWixAndStore;

        var firstSite = viewModel.SelectedServices.FirstOrDefault();
        var iconFilePath = default(string);

        if (firstSite != null)
        {
            linkName = firstSite.Id;

            iconFilePath = Path.Combine(
                sharedLocations.GetImageDirectoryPath(),
                $"{firstSite.Id}.ico");

            if (!File.Exists(iconFilePath))
                iconFilePath = null;
        }

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fullPath = Path.Combine(desktopPath, linkName + ".lnk");

        try
        {
            var shell = new WshShell();
            var shortcut = (WshShortcut)shell.CreateShortcut(fullPath);
            shortcut.TargetPath = targetPath;

            if (iconFilePath != null && File.Exists(iconFilePath))
                shortcut.IconLocation = iconFilePath;

            shortcut.Arguments = commandLineComposer.ComposeCommandLineArguments(viewModel, false);
            shortcut.Save();
        }
        catch (Exception ex)
        {
            appMessageBox.DisplayError(ex, false);
        }

        // Workaround - CJK 문자열을 CreateShortcut 호출 시 지정하지 못하는 문제 우회
        if (firstSite != null)
            File.Move(fullPath, Path.Combine(desktopPath, firstSite.DisplayName + ".lnk"));

        appMessageBox.DisplayInfo(InfoStrings.Info_ShortcutSuccess);
    }
}

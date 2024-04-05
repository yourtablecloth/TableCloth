using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Components.Implementations;

public sealed class ShortcutCrerator(
    ICommandLineComposer commandLineComposer,
    ISharedLocations sharedLocations,
    IAppMessageBox appMessageBox) : IShortcutCrerator
{
    public async Task<string?> CreateShortcutAsync(ITableClothViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var targetPath = sharedLocations.ExecutableFilePath;
        var linkName = CommonStrings.AppName;

        var firstSite = viewModel.SelectedServices.FirstOrDefault();
        var iconFilePath = default(string);

        if (firstSite != null)
        {
            linkName = firstSite.DisplayName;
            iconFilePath = sharedLocations.GetIconFilePath(firstSite.Id);

            if (!File.Exists(iconFilePath))
                iconFilePath = null;
        }

        var shortcutDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var shortcutFileName = linkName + ".lnk";

        var shortcutFilePath = Path.Combine(shortcutDirectoryPath, shortcutFileName);
        await File.WriteAllBytesAsync(shortcutFilePath, [], cancellationToken).ConfigureAwait(false);

        try
        {
            // https://stackoverflow.com/questions/13542005/create-shortcut-with-unicode-character
            // WshRuntimeLibrary의 경우 유니코드 문자열로 바로 가기 아이콘을 만들지 못하는 버그가 있음.
            var shellType = Type.GetTypeFromProgID("Shell.Application").EnsureNotNull("Cannot obtain Shell.Application type.");
            object oInstance = Activator.CreateInstance(shellType).EnsureNotNull("Cannot create instance of Shell.Application.");

            dynamic shell = oInstance;
            dynamic folder = shell.NameSpace(shortcutDirectoryPath);
            dynamic folderItem = folder.Items().Item(shortcutFileName);
            dynamic shortcut = folderItem.GetLink;

            shortcut.Path = targetPath;

            if (iconFilePath != null && File.Exists(iconFilePath))
                shortcut.SetIconLocation(iconFilePath, 0);

            shortcut.Arguments = commandLineComposer.ComposeCommandLineArguments(viewModel, false);
            shortcut.Description = $"{linkName}";
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Save();

            appMessageBox.DisplayInfo(InfoStrings.Info_ShortcutSuccess);
            return shortcutFilePath;
        }
        catch (Exception ex)
        {
            appMessageBox.DisplayError(ex, false);
            return default;
        }
    }

    public async Task<string?> CreateResponseFileAsync(ITableClothViewModel viewModel, CancellationToken cancellationToken = default)
    {
        var linkName = CommonStrings.AppName;
        var firstSite = viewModel.SelectedServices.FirstOrDefault();

        if (firstSite != null)
            linkName = firstSite.DisplayName;

        var shortcutDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var shortcutFileName = linkName + ".tclnk";

        var shortcutFilePath = Path.Combine(shortcutDirectoryPath, shortcutFileName);
        var fileContents = commandLineComposer.GetCommandLineExpressionList(viewModel, false);
        await File.WriteAllLinesAsync(shortcutFilePath, fileContents, cancellationToken).ConfigureAwait(false);
        return shortcutFilePath;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Interop;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Components.Implementations;

public sealed class ShortcutCreator(
    ICommandLineComposer commandLineComposer,
    ISharedLocations sharedLocations,
    IAppMessageBox appMessageBox) : IShortcutCreator
{
    public Task<string?> CreateShortcutAsync(DetailPageViewModel viewModel, CancellationToken cancellationToken = default)
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
        var shortcutFilePath = Path.Combine(shortcutDirectoryPath, linkName + ".lnk");

        try
        {
            // 기존 Shell.Application late-bound COM + dynamic (Native AOT 비호환)을 AOT 안전한
            // IShellLinkW/IPersistFile(GeneratedComInterface)로 대체. 유니코드 경로/아이콘도 정상 지원.
            NativeShellLink.Create(
                linkFilePath: shortcutFilePath,
                targetPath: targetPath,
                arguments: commandLineComposer.ComposeCommandLineArguments(viewModel, false),
                workingDirectory: Path.GetDirectoryName(targetPath),
                description: linkName,
                iconPath: iconFilePath,
                iconIndex: 0);

            appMessageBox.DisplayInfo(InfoStrings.Info_ShortcutSuccess);
            return Task.FromResult<string?>(shortcutFilePath);
        }
        catch (Exception ex)
        {
            appMessageBox.DisplayError(ex, false);
            return Task.FromResult<string?>(default);
        }
    }

    public async Task<string?> CreateResponseFileAsync(DetailPageViewModel viewModel, CancellationToken cancellationToken = default)
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

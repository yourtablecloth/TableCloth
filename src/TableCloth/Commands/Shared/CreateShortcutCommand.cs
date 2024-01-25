using System.Linq;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CreateShortcutCommand(
    IShortcutCrerator shortcutCrerator,
    IAppMessageBox appMessageBox) : ViewModelCommandBase<ITableClothViewModel>
{

    public override async void Execute(ITableClothViewModel viewModel)
    {
        if (!viewModel.SelectedServices.Any())
        {
            appMessageBox.DisplayError(ErrorStrings.Error_ShortcutNoSiteSelected, false);
            return;
        }

        if (viewModel.SelectedServices.Count() > 1)
            appMessageBox.DisplayInfo(InfoStrings.Info_WillCreateSingleSiteShortcut);

        await shortcutCrerator.CreateShortcutAsync(viewModel);
    }
}

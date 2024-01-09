using System.Linq;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CreateShortcutCommand(
    IShortcutCrerator shortcutCrerator,
    AppMessageBox appMessageBox) : ViewModelCommandBase<ITableClothViewModel>
{

    public override void Execute(ITableClothViewModel viewModel)
    {
        if (viewModel.SelectedServices.Count() < 1)
        {
            appMessageBox.DisplayError(StringResources.Error_ShortcutNoSiteSelected, false);
            return;
        }

        if (viewModel.SelectedServices.Count() > 1)
            appMessageBox.DisplayInfo(StringResources.Info_WillCreateSingleSiteShortcut);

        shortcutCrerator.CreateShortcut(viewModel);
    }
}

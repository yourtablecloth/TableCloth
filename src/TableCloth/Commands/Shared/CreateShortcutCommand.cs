using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System.Linq;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CreateShortcutCommand(
    IShortcutCrerator shortcutCrerator,
    IAppMessageBox appMessageBox) : ViewModelCommandBase<ITableClothViewModel>, IAsyncCommand<ITableClothViewModel>
{
    public override void Execute(ITableClothViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(ITableClothViewModel viewModel)
    {
        if (!viewModel.SelectedServices.Any())
        {
            appMessageBox.DisplayError(ErrorStrings.Error_ShortcutNoSiteSelected, false);
            return;
        }

        if (viewModel.SelectedServices.Count() > 1)
            appMessageBox.DisplayInfo(InfoStrings.Info_WillCreateSingleSiteShortcut);

        await shortcutCrerator.CreateShortcutAsync(viewModel);
        //await shortcutCrerator.CreateResponseFileAsync(viewModel);
    }
}

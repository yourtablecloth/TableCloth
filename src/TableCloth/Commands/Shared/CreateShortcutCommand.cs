using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CreateShortcutCommand(
    IShortcutCrerator shortcutCrerator) : ViewModelCommandBase<ITableClothViewModel>
{
    public override void Execute(ITableClothViewModel viewModel)
        => shortcutCrerator.CreateShortcut(viewModel);
}

using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CreateShortcutCommand : ViewModelCommandBase<ITableClothViewModel>
{
    public CreateShortcutCommand(
        IShortcutCrerator shortcutCrerator)
    {
        _shortcutCreator = shortcutCrerator;
    }

    private readonly IShortcutCrerator _shortcutCreator;

    public override void Execute(ITableClothViewModel viewModel)
        => _shortcutCreator.CreateShortcut(viewModel);
}

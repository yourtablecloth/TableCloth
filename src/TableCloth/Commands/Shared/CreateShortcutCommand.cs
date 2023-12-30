using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CreateShortcutCommand : ViewModelCommandBase<ITableClothViewModel>
{
    public CreateShortcutCommand(
        ShortcutCrerator shortcutCrerator)
    {
        _shortcutCreator = shortcutCrerator;
    }

    private readonly ShortcutCrerator _shortcutCreator;

    public override void Execute(ITableClothViewModel viewModel)
        => _shortcutCreator.CreateShortcut(viewModel);
}

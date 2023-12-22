using System;
using TableCloth.Components;
using TableCloth.Contracts;

namespace TableCloth.Commands;

public sealed class CreateShortcutCommand : CommandBase
{
    public CreateShortcutCommand(
        ShortcutCrerator shortcutCrerator)
    {
        _shortcutCreator = shortcutCrerator;
    }

    private readonly ShortcutCrerator _shortcutCreator;

    public override void Execute(object? parameter)
    {
        var viewModel = parameter as ICommandLineArgumentModel;

        if (viewModel == null)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        _shortcutCreator.CreateShortcut(viewModel);
    }
}

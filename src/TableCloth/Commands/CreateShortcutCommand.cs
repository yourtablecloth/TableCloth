using System;
using TableCloth.Components;
using TableCloth.Contracts;
using TableCloth.ViewModels;

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
        if (parameter is MainWindowViewModel viewModelV1)
            _shortcutCreator.CreateShortcutForV1(viewModelV1);
        else if (parameter is DetailPageViewModel viewModelV2)
            _shortcutCreator.CreateShortcutForV2(viewModelV2);
        else
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));        
    }
}

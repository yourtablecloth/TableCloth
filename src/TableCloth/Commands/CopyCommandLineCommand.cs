using System;
using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands;

public sealed class CopyCommandLineCommand : CommandBase
{
    public CopyCommandLineCommand(
        CommandLineComposer commandLineComposer,
        AppMessageBox appMessageBox)
    {
        _commandLineComposer = commandLineComposer;
        _appMessageBox = appMessageBox;
    }

    private readonly CommandLineComposer _commandLineComposer;
    private readonly AppMessageBox _appMessageBox;

    public override void Execute(object? parameter)
    {
        if (parameter is not DetailPageViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        var expression = _commandLineComposer.ComposeCommandLineExpression(viewModel, true);
        Clipboard.SetText(expression);

        _appMessageBox.DisplayInfo(StringResources.Info_CopyCommandLineSuccess, MessageBoxButton.OK);
    }
}

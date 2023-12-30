using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CopyCommandLineCommand : ViewModelCommandBase<ITableClothViewModel>
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

    public override void Execute(ITableClothViewModel viewModel)
    {
        var expression = _commandLineComposer.ComposeCommandLineExpression(viewModel, true);
        Clipboard.SetText(expression);

        _appMessageBox.DisplayInfo(StringResources.Info_CopyCommandLineSuccess, MessageBoxButton.OK);
    }
}

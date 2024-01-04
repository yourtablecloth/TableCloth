using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CopyCommandLineCommand : ViewModelCommandBase<ITableClothViewModel>
{
    public CopyCommandLineCommand(
        ICommandLineComposer commandLineComposer,
        IAppMessageBox appMessageBox)
    {
        _commandLineComposer = commandLineComposer;
        _appMessageBox = appMessageBox;
    }

    private readonly ICommandLineComposer _commandLineComposer;
    private readonly IAppMessageBox _appMessageBox;

    public override void Execute(ITableClothViewModel viewModel)
    {
        var expression = _commandLineComposer.ComposeCommandLineExpression(viewModel, true);
        Clipboard.SetText(expression);

        _appMessageBox.DisplayInfo(StringResources.Info_CopyCommandLineSuccess, MessageBoxButton.OK);
    }
}

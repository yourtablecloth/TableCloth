using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CopyCommandLineCommand(
    ICommandLineComposer commandLineComposer,
    IAppMessageBox appMessageBox) : ViewModelCommandBase<ITableClothViewModel>
{
    public override void Execute(ITableClothViewModel viewModel)
    {
        var expression = commandLineComposer.ComposeCommandLineExpression(viewModel, true);
        Clipboard.SetText(expression);

        appMessageBox.DisplayInfo(InfoStrings.Info_CopyCommandLineSuccess, MessageBoxButton.OK);
    }
}

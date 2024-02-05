using System.Runtime.InteropServices;
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

        try { Clipboard.SetDataObject(expression); }
        catch (ExternalException thrownException)
        {
            appMessageBox.DisplayError(
                StringResources.Error_With_Exception(ErrorStrings.Error_Cannot_CopyToClipboard, thrownException),
                false);
        }

        appMessageBox.DisplayInfo(InfoStrings.Info_CopyCommandLineSuccess, MessageBoxButton.OK);
    }
}

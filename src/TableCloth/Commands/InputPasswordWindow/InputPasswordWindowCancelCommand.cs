using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowCancelCommand : ViewModelCommandBase<InputPasswordWindowViewModel>
{
    public override void Execute(InputPasswordWindowViewModel viewModel)
    {
        viewModel.ValidatedCertPair = null;
        viewModel.RequestClose(this, new DialogRequestEventArgs(false));
    }
}

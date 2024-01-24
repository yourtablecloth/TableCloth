using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowCancelCommand : ViewModelCommandBase<InputPasswordWindowViewModel>
{
    public override async void Execute(InputPasswordWindowViewModel viewModel)
    {
        viewModel.ValidatedCertPair = null;
        await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(false));
    }
}

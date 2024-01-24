using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DisclaimerWindow;

public sealed class DisclaimerWindowLoadedCommand : ViewModelCommandBase<DisclaimerWindowViewModel>
{
    public override async void Execute(DisclaimerWindowViewModel viewModel)
        => await viewModel.NotifyViewLoadedAsync(this, EventArgs.Empty);
}

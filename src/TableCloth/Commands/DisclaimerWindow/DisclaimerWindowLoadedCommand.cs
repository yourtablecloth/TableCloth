using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DisclaimerWindow;

public sealed class DisclaimerWindowLoadedCommand : ViewModelCommandBase<DisclaimerWindowViewModel>
{
    public override void Execute(DisclaimerWindowViewModel viewModel)
        => viewModel.NotifyViewLoaded(this, EventArgs.Empty);
}

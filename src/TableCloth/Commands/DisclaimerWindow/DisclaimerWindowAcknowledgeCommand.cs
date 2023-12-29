using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DisclaimerWindow;

public sealed class DisclaimerWindowAcknowledgeCommand : ViewModelCommandBase<DisclaimerWindowViewModel>
{
    public override void Execute(DisclaimerWindowViewModel viewModel)
        => viewModel.NotifyDisclaimerAcknowledged(this, EventArgs.Empty);
}

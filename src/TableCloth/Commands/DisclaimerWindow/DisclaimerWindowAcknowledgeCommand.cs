using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DisclaimerWindow;

public sealed class DisclaimerWindowAcknowledgeCommand : ViewModelCommandBase<DisclaimerWindowViewModel>
{
    public override async void Execute(DisclaimerWindowViewModel viewModel)
        => await viewModel.NotifyDisclaimerAcknowledgedAsync(this, EventArgs.Empty);
}

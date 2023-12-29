using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DisclaimerWindow;

public sealed class DisclaimerWindowAcknowledgeCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        if (parameter is not DisclaimerWindowViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        viewModel.NotifyDisclaimerAcknowledged(this, EventArgs.Empty);
    }
}

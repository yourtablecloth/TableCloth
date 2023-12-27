using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DisclaimerWindow;

public sealed class DisclaimerWindowAcknowledgeCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        var viewModel = parameter as DisclaimerWindowViewModel;

        if (viewModel == null)
            throw new ArgumentException(nameof(viewModel));

        viewModel.NotifyDisclaimerAcknowledged(this, EventArgs.Empty);
    }
}

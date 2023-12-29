using System;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowCancelCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        if (parameter is not InputPasswordWindowViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        viewModel.ValidatedCertPair = null;
        viewModel.RequestClose(this, new DialogRequestEventArgs(false));
    }
}

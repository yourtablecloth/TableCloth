using System;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowCancelCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        var viewModel = parameter as InputPasswordWindowViewModel;

        if (viewModel == null)
            throw new ArgumentException(nameof(parameter));

        viewModel.ValidatedCertPair = null;
        viewModel.RequestClose(this, new DialogRequestEventArgs(false));
    }
}

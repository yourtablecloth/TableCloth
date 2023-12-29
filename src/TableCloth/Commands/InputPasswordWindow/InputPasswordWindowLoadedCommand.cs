using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowLoadedCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        if (parameter is not InputPasswordWindowViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        viewModel.NotifyViewLoaded(this, EventArgs.Empty);
    }
}

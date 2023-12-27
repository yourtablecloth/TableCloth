using System;
using System.Text;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowLoadedCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        var inputPasswordWindowViewModel = parameter as InputPasswordWindowViewModel;

        if (inputPasswordWindowViewModel == null)
            throw new ArgumentException(nameof(inputPasswordWindowViewModel));

        inputPasswordWindowViewModel.NotifyViewLoaded(this, EventArgs.Empty);
    }
}

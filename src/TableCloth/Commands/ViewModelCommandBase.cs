using System;

namespace TableCloth.Commands;

public abstract class ViewModelCommandBase<TViewModel> : CommandBase
    where TViewModel : class
{
    public override void Execute(object? parameter)
    {
        var viewModel = parameter as TViewModel;
        ArgumentNullException.ThrowIfNull(viewModel);
        Execute(viewModel);
    }

    public abstract void Execute(TViewModel viewModel);
}

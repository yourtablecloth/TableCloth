using System;

namespace TableCloth.Commands;

public abstract class ViewModelCommandBase<TViewModel> : CommandBase
    where TViewModel : class
{
    public override void Execute(object? parameter)
    {
        Execute(parameter.EnsureArgumentNotNullWithCast<object, TViewModel>(
            "Selected parameter is not a supported type.", nameof(parameter)));
    }

    public abstract void Execute(TViewModel viewModel);
}

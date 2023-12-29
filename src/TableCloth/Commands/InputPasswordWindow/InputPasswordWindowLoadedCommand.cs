using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowLoadedCommand : ViewModelCommandBase<InputPasswordWindowViewModel>
{
    public override void Execute(InputPasswordWindowViewModel viewModel)
        => viewModel.NotifyViewLoaded(this, EventArgs.Empty);
}

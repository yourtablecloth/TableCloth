using System;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowLoadedCommand : ViewModelCommandBase<InputPasswordWindowViewModel>
{
    public override async void Execute(InputPasswordWindowViewModel viewModel)
        => await viewModel.NotifyViewLoadedAsync(this, EventArgs.Empty);
}

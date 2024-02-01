using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.Threading.Tasks;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DisclaimerWindow;

public sealed class DisclaimerWindowAcknowledgeCommand : ViewModelCommandBase<DisclaimerWindowViewModel>, IAsyncCommand<DisclaimerWindowViewModel>
{
    public override void Execute(DisclaimerWindowViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(DisclaimerWindowViewModel viewModel)
        => await viewModel.NotifyDisclaimerAcknowledgedAsync(this, EventArgs.Empty);
}

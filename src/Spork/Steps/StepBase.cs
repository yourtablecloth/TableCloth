using Spork.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps
{
    public abstract class StepBase<TInstallItemViewModel> : IStep<TInstallItemViewModel>, IStep
        where TInstallItemViewModel : InstallItemViewModel
    {
        public abstract Task LoadContentForStepAsync(TInstallItemViewModel viewModel, CancellationToken cancellationToken = default);

        public abstract Task PlayStepAsync(TInstallItemViewModel viewModel, CancellationToken cancellationToken = default);

        public abstract bool ShouldSimulateWhenDryRun { get; }

        Task IStep.LoadContentForStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken)
            => LoadContentForStepAsync((TInstallItemViewModel)viewModel, cancellationToken);

        Task IStep.PlayStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken)
            => PlayStepAsync((TInstallItemViewModel)viewModel, cancellationToken);
    }
}

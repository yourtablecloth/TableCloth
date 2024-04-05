using Spork.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps
{
    public abstract class StepBase<TInstallItemViewModel> : IStep<TInstallItemViewModel>, IStep
        where TInstallItemViewModel : InstallItemViewModel
    {
        public abstract Task<bool> EvaluateRequiredStepAsync(TInstallItemViewModel viewModel, CancellationToken cancellationToken = default);

        public abstract Task LoadContentForStepAsync(TInstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default);

        public abstract Task PlayStepAsync(TInstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default);

        public abstract bool ShouldSimulateWhenDryRun { get; }

        Task<bool> IStep.EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken)
            => EvaluateRequiredStepAsync((TInstallItemViewModel)viewModel, cancellationToken);

        Task IStep.LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken)
            => LoadContentForStepAsync((TInstallItemViewModel)viewModel, progressCallback, cancellationToken);

        Task IStep.PlayStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken)
            => PlayStepAsync((TInstallItemViewModel)viewModel, progressCallback, cancellationToken);
    }
}

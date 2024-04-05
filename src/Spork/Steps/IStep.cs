using Spork.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps
{
    public interface IStep<TInstallItemViewModel> : IStep
        where TInstallItemViewModel : InstallItemViewModel
    {
        Task<bool> EvaluateRequiredStepAsync(TInstallItemViewModel viewModel, CancellationToken cancellationToken = default);

        Task LoadContentForStepAsync(TInstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default);

        Task PlayStepAsync(TInstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default);
    }

    public interface IStep
    {
        Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default);

        Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default);

        Task PlayStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default);

        bool ShouldSimulateWhenDryRun { get; }
    }
}

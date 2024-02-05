using Hostess.ViewModels;
using System.Threading;
using System.Threading.Tasks;

namespace Hostess.Steps
{
    public interface IStep<TInstallItemViewModel> : IStep
        where TInstallItemViewModel : InstallItemViewModel
    {
        Task LoadContentForStepAsync(TInstallItemViewModel viewModel, CancellationToken cancellationToken = default);

        Task PlayStepAsync(TInstallItemViewModel viewModel, CancellationToken cancellationToken = default);
    }

    public interface IStep
    {
        Task LoadContentForStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default);

        Task PlayStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default);

        bool ShouldSimulateWhenDryRun { get; }
    }
}

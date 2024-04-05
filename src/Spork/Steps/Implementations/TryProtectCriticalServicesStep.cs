using Spork.Components;
using Spork.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps.Implementations
{
    public sealed class TryProtectCriticalServicesStep : StepBase<InstallItemViewModel>
    {
        public TryProtectCriticalServicesStep(
            ICriticalServiceProtector criticalServiceProtector,
            IAppMessageBox appMessageBox)
        {
            _criticalServiceProtector = criticalServiceProtector;
            _appMessageBox = appMessageBox;
        }

        private readonly ICriticalServiceProtector _criticalServiceProtector;
        private readonly IAppMessageBox _appMessageBox;

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            try { _criticalServiceProtector.PreventServiceProcessTermination("TermService"); }
            catch (AggregateException aex) { _appMessageBox.DisplayError(aex.InnerException, false); }
            catch (Exception ex) { _appMessageBox.DisplayError(ex, false); }

            try { _criticalServiceProtector.PreventServiceStop("TermService", Environment.UserName); }
            catch (AggregateException aex) { _appMessageBox.DisplayError(aex.InnerException, false); }
            catch (Exception ex) { _appMessageBox.DisplayError(ex, false); }

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}

using Spork.Steps;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.ViewModels;

namespace Spork.ViewModels
{
    public class StepItemViewModelForDesigner : StepItemViewModel
    {
        internal protected sealed class FakeStepForDesigner : StepBase<InstallItemViewModel>
        {
            public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public override Task PlayStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public override bool ShouldSimulateWhenDryRun
                => false;

            public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }

        public StepItemViewModelForDesigner()
        {
            Step = new FakeStepForDesigner();
        }
    }

    public class StepItemViewModel : ViewModelBase
    {
        private InstallItemViewModel _argument;
        private IStep _step;
        private string _targetSiteName;
        private string _targetSiteUrl;
        private string _packageName;
        private bool? _installed;
        private string _statusMessage;
        private string _errorMessage;

        public InstallItemViewModel Argument
        {
            get => _argument;
            set => SetProperty(ref _argument, value);
        }

        public IStep Step
        {
            get => _step;
            set => SetProperty(ref _step, value);
        }

        public string TargetSiteName
        {
            get => _targetSiteName;
            set => SetProperty(ref _targetSiteName, value);
        }

        public string TargetSiteUrl
        {
            get => _targetSiteUrl;
            set => SetProperty(ref _targetSiteUrl, value);
        }

        public string PackageName
        {
            get => _packageName;
            set => SetProperty(ref _packageName, value);
        }

        public bool? Installed
        {
            get => _installed;
            set => SetProperty(ref _installed, value, new string[] { nameof(Installed), nameof(StatusMessage), nameof(ErrorMessage), nameof(InstallFlags), nameof(ShowErrorMessageLink), });
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value, new string[] { nameof(StatusMessage), nameof(Installed), nameof(ErrorMessage), nameof(InstallFlags), nameof(ShowErrorMessageLink), });
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value, new string[] { nameof(ErrorMessage), nameof(StatusMessage), nameof(Installed), nameof(InstallFlags), nameof(ShowErrorMessageLink), });
        }

        public bool ShowErrorMessageLink
            => !string.IsNullOrWhiteSpace(_errorMessage) && _installed.HasValue && !_installed.Value;

        public string InstallFlags
            => $"{(_installed.HasValue ? _installed.Value ? "\u2714\uFE0F" : "\u274C\uFE0F" : "\u23F3\uFE0F")}";

        public override string ToString()
            => $"{InstallFlags} {TargetSiteName} {PackageName} {StatusMessage}";
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using Spork.Steps;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.ViewModels
{
    public partial class StepItemViewModelForDesigner : StepItemViewModel
    {
        internal protected sealed class FakeStepForDesigner : StepBase<InstallItemViewModel>
        {
            public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public override Task PlayStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
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

    public partial class StepItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private InstallItemViewModel _argument;

        [ObservableProperty]
        private IStep _step;

        [ObservableProperty]
        private string _targetSiteName;

        [ObservableProperty]
        private string _targetSiteUrl;

        [ObservableProperty]
        private string _packageName;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusMessage))]
        [NotifyPropertyChangedFor(nameof(ErrorMessage))]
        [NotifyPropertyChangedFor(nameof(InstallFlags))]
        [NotifyPropertyChangedFor(nameof(ShowErrorMessageLink))]
        private bool? _installed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Installed))]
        [NotifyPropertyChangedFor(nameof(ErrorMessage))]
        [NotifyPropertyChangedFor(nameof(InstallFlags))]
        [NotifyPropertyChangedFor(nameof(ShowErrorMessageLink))]
        private string _statusMessage;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusMessage))]
        [NotifyPropertyChangedFor(nameof(Installed))]
        [NotifyPropertyChangedFor(nameof(InstallFlags))]
        [NotifyPropertyChangedFor(nameof(ShowErrorMessageLink))]
        private string _errorMessage;

        [ObservableProperty]
        private double _progressRate;

        [ObservableProperty]
        private bool _showProgress;

        public bool ShowErrorMessageLink
            => !string.IsNullOrWhiteSpace(ErrorMessage) && Installed.HasValue && !Installed.Value;

        public string InstallFlags
            => $"{(Installed.HasValue ? Installed.Value ? "\u2714\uFE0F" : "\u274C\uFE0F" : "\u23F3\uFE0F")}";

        public override string ToString()
            => $"{InstallFlags} {TargetSiteName} {PackageName} {StatusMessage}";
    }
}

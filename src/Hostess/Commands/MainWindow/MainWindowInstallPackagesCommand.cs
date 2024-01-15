using Hostess.Components;
using Hostess.ViewModels;

namespace Hostess.Commands.MainWindow
{
    public sealed class MainWindowInstallPackagesCommand : ViewModelCommandBase<MainWindowViewModel>
    {
        public MainWindowInstallPackagesCommand(
            IStepsPlayer stepsPlayer)
        {
            _stepsPlayer = stepsPlayer;
        }

        private readonly IStepsPlayer _stepsPlayer;

        protected override bool EvaluateCanExecute()
            => !_stepsPlayer.IsRunning;

        public override async void Execute(MainWindowViewModel viewModel)
        {
            var hasAnyFailure = await _stepsPlayer.PlayStepsAsync(
                viewModel.InstallItems,
                viewModel.ShowDryRunNotification);

            if (!hasAnyFailure)
                viewModel.RequestClose(this);
        }
    }
}

using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Spork.Steps;
using Spork.ViewModels;
using System;
using System.Threading.Tasks;

namespace Spork.Commands.MainWindow
{
    public sealed class MainWindowInstallPackagesCommand : ViewModelCommandBase<MainWindowViewModel>, IAsyncCommand<MainWindowViewModel>
    {
        public MainWindowInstallPackagesCommand(
            IStepsPlayer stepsPlayer)
        {
            _stepsPlayer = stepsPlayer;
        }

        private readonly IStepsPlayer _stepsPlayer;

        protected override bool EvaluateCanExecute()
            => !_stepsPlayer.IsRunning;

        public override void Execute(MainWindowViewModel viewModel)
            => ExecuteAsync(viewModel).SafeFireAndForget();

        // 뷰 모델과 연결된 이벤트 통지기를 호출할 때는 Dispatcher를 통해서 호출하도록 코드 수정이 필요함.
        public async Task ExecuteAsync(MainWindowViewModel viewModel)
        {
            var hasAnyFailure = await _stepsPlayer.PlayStepsAsync(
                viewModel.InstallSteps,
                viewModel.ShowDryRunNotification);

            if (!hasAnyFailure)
                await viewModel.RequestCloseAsync(this, EventArgs.Empty);
        }
    }
}

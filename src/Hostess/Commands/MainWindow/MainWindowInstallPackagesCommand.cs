using Hostess.Components;
using Hostess.ViewModels;
using System;

namespace Hostess.Commands.MainWindow
{
    public sealed class MainWindowInstallPackagesCommand : ViewModelCommandBase<MainWindowViewModel>
    {
        public MainWindowInstallPackagesCommand(
            IApplicationService applicationService,
            IStepsPlayer stepsPlayer)
        {
            _applicationService = applicationService;
            _stepsPlayer = stepsPlayer;
        }

        private readonly IApplicationService _applicationService;
        private readonly IStepsPlayer _stepsPlayer;

        protected override bool EvaluateCanExecute()
            => !_stepsPlayer.IsRunning;

        // 뷰 모델과 연결된 이벤트 통지기를 호출할 때는 Dispatcher를 통해서 호출하도록 코드 수정이 필요함.
        public override async void Execute(MainWindowViewModel viewModel)
        {
            var hasAnyFailure = await _stepsPlayer.PlayStepsAsync(
                viewModel.InstallItems,
                viewModel.ShowDryRunNotification);

            if (!hasAnyFailure)
                await viewModel.RequestCloseAsync(this, EventArgs.Empty);
        }
    }
}

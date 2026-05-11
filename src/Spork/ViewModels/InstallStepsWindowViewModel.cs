using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Spork.Components;
using Spork.Steps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.Resources;

namespace Spork.ViewModels
{
    public partial class InstallStepsWindowViewModelForDesigner : InstallStepsWindowViewModel
    {
        public IList<StepItemViewModelForDesigner> InstallStepsForDesigner
            => DesignTimeResources.DesignTimePackageInformations.Select((x, i) =>
            {
                var triState = DesignTimeResources.ConvertToTriState(i);
                return new StepItemViewModelForDesigner
                {
                    Argument = new InstallItemViewModel(),
                    TargetSiteName = "Sample Site",
                    TargetSiteUrl = "https://www.example.com/",
                    PackageName = x.Name,
                    Installed = triState,
                    StatusMessage = "Status",
                    ErrorMessage = DesignTimeResources.GenerateRandomErrorMessage(i),
                    ProgressRate = triState.HasValue ? 100d : 50d,
                    ShowProgress = !triState.HasValue,
                };
            }).ToList();
    }

    public partial class InstallStepsWindowViewModel : ObservableObject
    {
        protected InstallStepsWindowViewModel() { }

        [ActivatorUtilitiesConstructor]
        public InstallStepsWindowViewModel(
            IStepsPlayer stepsPlayer,
            IAppMessageBox appMessageBox,
            TaskFactory taskFactory)
        {
            _stepsPlayer = stepsPlayer;
            _appMessageBox = appMessageBox;
            _taskFactory = taskFactory;
        }

        private readonly IStepsPlayer _stepsPlayer;
        private readonly IAppMessageBox _appMessageBox;
        private readonly TaskFactory _taskFactory;

        /// <summary>
        /// 모달을 띄우기 직전에 호출 측이 채워 넣는 설치 단계 목록입니다.
        /// </summary>
        public IList<StepItemViewModel> InstallSteps { get; set; } = new List<StepItemViewModel>();

        /// <summary>
        /// 호출 측이 채워 넣는 dry-run 플래그입니다.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// 설치가 성공한 경우 true, 실패한 경우 false. 아직 실행되지 않았으면 null.
        /// </summary>
        public bool? Succeeded { get; private set; }

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        [ObservableProperty]
        private bool _showCloseButton;

        [RelayCommand]
        private void ShowErrorMessage(string errorMessage)
        {
            _appMessageBox.DisplayError(errorMessage, true);
        }

        [RelayCommand]
        private async Task InstallStepsWindowLoaded()
        {
            if (InstallSteps == null || InstallSteps.Count == 0)
            {
                Succeeded = true;
                await RaiseCloseAsync(true);
                return;
            }

            var hasFailure = await _stepsPlayer.PlayStepsAsync(InstallSteps, DryRun);
            Succeeded = !hasFailure;

            if (!hasFailure)
            {
                // 사용자가 100% 완료된 상태를 잠깐 확인할 수 있도록 짧은 지연 후 자동 닫기.
                await Task.Delay(TimeSpan.FromMilliseconds(800));
                await RaiseCloseAsync(true);
            }
            else
            {
                // 실패: 사용자가 결과를 확인하고 직접 닫을 수 있도록 닫기 버튼을 노출.
                ShowCloseButton = true;
            }
        }

        [RelayCommand]
        private async Task CloseDialog()
        {
            await RaiseCloseAsync(Succeeded ?? false);
        }

        private async Task RaiseCloseAsync(bool result)
        {
            await _taskFactory.StartNew(
                () => CloseRequested?.Invoke(this, new DialogRequestEventArgs(result)),
                default(CancellationToken)).ConfigureAwait(false);
        }
    }
}

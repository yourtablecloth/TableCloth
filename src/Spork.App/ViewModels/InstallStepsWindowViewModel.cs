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
        private CancellationTokenSource _installCts;

        /// <summary>
        /// 모달을 띄우기 직전에 호출 측이 채워 넣는 설치 단계 목록입니다.
        /// </summary>
        public IList<StepItemViewModel> InstallSteps { get; set; } = new List<StepItemViewModel>();

        /// <summary>
        /// 호출 측이 채워 넣는 dry-run 플래그입니다.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// 모달 상단에 표시할 대상 이름(예: 사이트 표시명). null이면 영역이 숨겨집니다.
        /// </summary>
        [ObservableProperty]
        private string _targetTitle;

        /// <summary>
        /// 모달 상단의 아이콘 해석에 사용되는 키(보통 서비스 ID). ServiceLogoConverter가 이미지로 변환합니다.
        /// </summary>
        [ObservableProperty]
        private string _targetIconKey;

        /// <summary>
        /// 설치 진행 상태:
        /// <list type="bullet">
        ///   <item><see langword="true"/> — 모든 단계 성공.</item>
        ///   <item><see langword="false"/> — 일부/전체 단계 실패. 사용자가 결과 화면을 본 뒤 닫음.</item>
        ///   <item><see langword="null"/> — 진행 중 또는 사용자가 모달을 닫아 취소함.</item>
        /// </list>
        /// 호출 측은 <c>HasValue</c> 로 "시도된 설치" 여부를 판정한다. 시도된 설치(성공/실패 모두)는
        /// 사이트가 열려야 하지만, 사용자가 명시적으로 취소했다면 열지 않는다.
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

            _installCts = new CancellationTokenSource();
            var token = _installCts.Token;

            try
            {
                var hasFailure = await _stepsPlayer.PlayStepsAsync(InstallSteps, DryRun, token).ConfigureAwait(true);
                Succeeded = !hasFailure;

                if (!hasFailure)
                {
                    // 사용자가 100% 완료된 상태를 잠깐 확인할 수 있도록 짧은 지연 후 자동 닫기.
                    // 지연 자체도 취소 대상이라 사용자가 도중에 닫으면 즉시 빠진다.
                    await Task.Delay(TimeSpan.FromMilliseconds(800), token).ConfigureAwait(true);
                    await RaiseCloseAsync(true);
                }
                else
                {
                    // 실패: 사용자가 결과를 확인하고 직접 닫을 수 있도록 닫기 버튼을 노출.
                    ShowCloseButton = true;
                }
            }
            catch (OperationCanceledException)
            {
                // 사용자가 창을 닫아 취소된 케이스. Window는 이미 닫히는 중이므로 추가 동작 없이 빠진다.
                // Succeeded 는 null 로 유지하여 호출 측이 "시도된 실패(false)"와 "취소(null)"를 구분할
                // 수 있게 한다 — 실패는 사이트가 열려야 하지만 취소는 사용자 의사 존중.
            }
        }

        /// <summary>
        /// 진행 중인 설치 단계를 취소합니다. <see cref="InstallStepsWindow"/>의 Closing 이벤트에서 호출됩니다.
        /// 이미 완료/취소된 경우는 안전하게 무시됩니다.
        /// </summary>
        public void CancelInstall()
        {
            try { _installCts?.Cancel(); }
            catch (ObjectDisposedException) { /* 이미 dispose 된 경우 */ }
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

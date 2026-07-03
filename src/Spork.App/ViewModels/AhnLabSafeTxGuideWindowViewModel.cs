using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Spork.Components;
using System;
using System.Diagnostics;
using TableCloth.Events;

namespace Spork.ViewModels
{
    public partial class AhnLabSafeTxGuideWindowViewModelForDesigner : AhnLabSafeTxGuideWindowViewModel { }

    /// <summary>
    /// AhnLab Safe Transaction(ASTx)의 "원격 접속 차단"을 사이트 접속 전에 해제하도록 안내하는 전용 창의 뷰모델(이슈 #275).
    /// - 설정 제어판(StSess.exe /config)을 여는 버튼을 제공하고,
    /// - 사용자가 해제를 완료했음을 명시적으로 확인(체크)해야만 '계속'이 활성화되는 확인 게이트를 둔다.
    /// 실제 ASTx 설정값은 암호화되어 외부에서 검증/변경할 수 없어(제품 설계상) 확인란은 사용자 확인용이다.
    /// </summary>
    public partial class AhnLabSafeTxGuideWindowViewModel : ObservableObject
    {
        protected AhnLabSafeTxGuideWindowViewModel() { }

        [ActivatorUtilitiesConstructor]
        public AhnLabSafeTxGuideWindowViewModel(IAppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly IAppMessageBox _appMessageBox;

        /// <summary>ASTx 설정 실행 파일(StSess.exe) 경로. 호출 측(AppUserInterface)이 주입한다.</summary>
        public string StSessPath { get; set; }

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        // A: 확인 게이트. 사용자가 해제 완료를 체크해야 '계속'이 활성화된다.
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
        private bool _isConfirmed;

        /// <summary>ASTx 설정 제어판을 연다(다시 열기 포함).</summary>
        [RelayCommand]
        private void OpenSettings()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(StSessPath))
                    return;

                Process.Start(new ProcessStartInfo(StSessPath, "/config") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _appMessageBox?.DisplayError(ex, false);
            }
        }

        private bool CanContinue() => IsConfirmed;

        [RelayCommand(CanExecute = nameof(CanContinue))]
        private void Continue()
            => CloseRequested?.Invoke(this, new DialogRequestEventArgs(true));

        [RelayCommand]
        private void Skip()
            => CloseRequested?.Invoke(this, new DialogRequestEventArgs(false));
    }
}

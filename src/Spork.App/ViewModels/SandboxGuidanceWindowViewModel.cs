using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.Resources;

namespace Spork.ViewModels
{
    public partial class SandboxGuidanceWindowViewModelForDesigner : SandboxGuidanceWindowViewModel { }

    // 이슈: Spork 단독 실행(비-WDAGUtilityAccount)일 때 사이트 실행 전 "Windows Sandbox 환경이 아닐 수 있음"을
    // 안내하는 다이얼로그의 VM. '다시 보지 않기' 체크 상태를 노출하고, 호출 측이 닫힘 후 이를 읽어 영속화한다.
    public partial class SandboxGuidanceWindowViewModel : ObservableObject
    {
        protected SandboxGuidanceWindowViewModel() { }

        [ActivatorUtilitiesConstructor]
        public SandboxGuidanceWindowViewModel(
            TaskFactory taskFactory)
        {
            _taskFactory = taskFactory;
        }

        private readonly TaskFactory _taskFactory;

        [ObservableProperty]
        private bool _doNotShowAgain = false;

        [RelayCommand]
        private void OpenHomepage()
        {
            try
            {
                Process.Start(new ProcessStartInfo(CommonStrings.AppInfoUrl) { UseShellExecute = true });
            }
            catch
            {
                // 브라우저 실행 실패가 안내 흐름을 막지 않도록 무시.
            }
        }

        [RelayCommand]
        private Task Continue()
            => _taskFactory.StartNew(
                () => CloseRequested?.Invoke(this, new DialogRequestEventArgs(true)),
                default(CancellationToken));

        public event EventHandler<DialogRequestEventArgs> CloseRequested;
    }
}

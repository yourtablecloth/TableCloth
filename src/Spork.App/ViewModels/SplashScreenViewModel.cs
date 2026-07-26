using CommunityToolkit.Mvvm.ComponentModel;
using TableCloth;
using TableCloth.Resources;

namespace Spork.ViewModels
{
    // 이슈 #296: TableCloth 와 동일한 스플래시(빨간 식탁보)를 Spork 에도 적용. Spork 는 자체 업데이트가 없어
    // TableCloth 스플래시의 업데이트 다운로드 진행은 두지 않고, 버전/상태 + 무한 진행바만 노출한다.
    public partial class SplashScreenViewModel : ObservableObject
    {
        public SplashScreenViewModel()
        {
            _appVersion = Helpers.GetAppVersion();
        }

        [ObservableProperty]
        private string _appVersion = string.Empty;

        [ObservableProperty]
        private string _status = UIStringResources.Status_PleaseWait;
    }
}

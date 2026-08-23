using Avalonia.Controls;
using Avalonia.Interactivity;
using TableCloth.Resources;

namespace Spork.Dialogs
{
    /// <summary>
    /// [미리 보기] 유휴 자동 종료 직전에 표시하는 경고 창(이슈 #197). 남은 초를 카운트다운으로 보여주고,
    /// 사용자가 다시 활동하면(입력 발생) 모니터가 이 창을 닫는다. "계속 사용" 버튼 클릭 자체도 입력이라
    /// 유휴 시간이 리셋되므로, 버튼은 창을 닫기만 한다.
    /// </summary>
    public partial class IdleLogoutWarningWindow : Window
    {
        public IdleLogoutWarningWindow()
        {
            InitializeComponent();
        }

        /// <summary>남은 초를 갱신한다. 모니터가 매 tick 호출한다.</summary>
        public void SetRemaining(int seconds)
            => MessageText.Text = string.Format(UIStringResources.Sandbox_IdleLogout_WarningMessage, seconds);

        private void KeepUsingButton_Click(object? sender, RoutedEventArgs e)
            => Close();
    }
}

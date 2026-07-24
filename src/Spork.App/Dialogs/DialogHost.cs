using Avalonia.Controls;
using Avalonia.Threading;
using System.Threading.Tasks;

namespace Spork.Dialogs
{
    /// <summary>
    /// 이슈 #296: WPF <c>Window.ShowDialog()</c>(동기, <c>DialogResult</c> 반환)의 대체. Avalonia 의
    /// <see cref="Window.ShowDialog{TResult}(Window)"/> 는 비동기이므로, <see cref="Dispatcher.PushFrame"/> 로
    /// 중첩 루프를 돌려 동기 블로킹 시맨틱을 재현한다. 반드시 UI 스레드에서 호출한다.
    /// </summary>
    internal static class DialogHost
    {
        public static bool? ShowModal(Window window, Window? owner)
        {
            bool? result = null;
            var frame = new DispatcherFrame();

            if (owner != null)
            {
                window.ShowDialog<bool?>(owner).ContinueWith(t =>
                {
                    result = t.Status == TaskStatus.RanToCompletion ? t.Result : null;
                    frame.Continue = false;
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
            else
            {
                window.Closed += (_, _) => frame.Continue = false;
                window.Show();
            }

            Dispatcher.UIThread.PushFrame(frame);
            return result;
        }
    }
}

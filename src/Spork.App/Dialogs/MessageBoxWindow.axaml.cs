using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System;
using TableCloth.Models;

namespace Spork.Dialogs
{
    /// <summary>
    /// 이슈 #296: WPF <c>MessageBox.Show</c> 대체. Avalonia 는 동기 모달 메시지 상자를 기본 제공하지 않으므로,
    /// 의존성 추가 없이 자작한다. <see cref="Dispatcher.PushFrame(DispatcherFrame)"/> 로 중첩 디스패처 루프를 돌려
    /// WPF 와 동일한 <b>동기 블로킹</b> 시맨틱을 재현한다(호출부의 async 리플을 피하기 위함).
    /// </summary>
    public partial class MessageBoxWindow : Window
    {
        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        public MessageBoxWindow(string? message, string? caption, AppMessageBoxButton button,
            AppMessageBoxImage icon, AppMessageBoxResult defaultResult)
            : this()
        {
            Title = caption ?? string.Empty;
            MessageText.Text = message ?? string.Empty;
            Result = defaultResult;

            IconGlyph.Text = icon switch
            {
                AppMessageBoxImage.Information => "ⓘ", // ⓘ
                AppMessageBoxImage.Warning => "⚠",     // ⚠
                AppMessageBoxImage.Error => "⛔",       // ⛔
                AppMessageBoxImage.Question => "❓",    // ❓
                _ => string.Empty,
            };

            AddButtons(button, defaultResult);
        }

        public AppMessageBoxResult Result { get; private set; }

        private void AddButtons(AppMessageBoxButton button, AppMessageBoxResult defaultResult)
        {
            switch (button)
            {
                case AppMessageBoxButton.OKCancel:
                    AddButton("확인", AppMessageBoxResult.OK, defaultResult, isCancel: false);
                    AddButton("취소", AppMessageBoxResult.Cancel, defaultResult, isCancel: true);
                    break;
                case AppMessageBoxButton.YesNo:
                    AddButton("예", AppMessageBoxResult.Yes, defaultResult, isCancel: false);
                    AddButton("아니오", AppMessageBoxResult.No, defaultResult, isCancel: false);
                    break;
                case AppMessageBoxButton.YesNoCancel:
                    AddButton("예", AppMessageBoxResult.Yes, defaultResult, isCancel: false);
                    AddButton("아니오", AppMessageBoxResult.No, defaultResult, isCancel: false);
                    AddButton("취소", AppMessageBoxResult.Cancel, defaultResult, isCancel: true);
                    break;
                default:
                    AddButton("확인", AppMessageBoxResult.OK, defaultResult, isCancel: true);
                    break;
            }
        }

        private void AddButton(string text, AppMessageBoxResult result, AppMessageBoxResult defaultResult, bool isCancel)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 84,
                Padding = new Thickness(14, 6),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                IsDefault = result == defaultResult,
                IsCancel = isCancel,
            };
            button.Click += (_, _) =>
            {
                Result = result;
                Close();
            };
            ButtonsPanel.Children.Add(button);
        }

        /// <summary>
        /// UI 스레드에서 호출해야 한다. 소유자가 있으면 모달로, 없으면(스타트업 등) 중앙에 띄우고,
        /// 창이 닫힐 때까지 중첩 프레임을 돌려 동기적으로 결과를 반환한다.
        /// </summary>
        public static AppMessageBoxResult ShowModal(Window? owner, string? message, string? caption,
            AppMessageBoxButton button, AppMessageBoxImage icon, AppMessageBoxResult defaultResult)
        {
            var window = new MessageBoxWindow(message, caption, button, icon, defaultResult);
            TableCloth.Controls.WindowChrome.RemoveMinimizeBox(window); // 이슈 #296: 모달 메시지 상자에 최소화 버튼 제거
            var frame = new DispatcherFrame();
            window.Closed += (_, _) => frame.Continue = false;

            if (owner != null)
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _ = window.ShowDialog(owner);
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Show();
            }

            Dispatcher.UIThread.PushFrame(frame);
            return window.Result;
        }
    }
}

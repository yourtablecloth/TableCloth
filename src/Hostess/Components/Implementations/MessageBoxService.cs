using System;
using System.Windows;
using System.Windows.Threading;

namespace Hostess.Components.Implementations
{
    public sealed class MessageBoxService : IMessageBoxService
    {
        public MessageBoxService(
            Application application)
        {
            _application = application;
        }

        private readonly Application _application;

        public MessageBoxResult Show(Window
#if !NETFX
?
#endif
            owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options = default)
        {
            var dispatcher = _application.Dispatcher ?? Dispatcher.CurrentDispatcher;

            if (owner == null)
                owner = _application.MainWindow;

            // owner 파라미터를 null 참조로 지정하더라도 Windows Forms 처럼 parent-less 메시지 박스를 만들어주지는 않음.
            // GH-121 fix
            if (owner != null)
            {
                return (MessageBoxResult)dispatcher.Invoke(
                    new Func<Window, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult, MessageBoxOptions, MessageBoxResult>(MessageBox.Show),
                    new object[] { owner, messageBoxText, caption, button, icon, defaultResult, options });
            }
            else
            {
                return (MessageBoxResult)dispatcher.Invoke(
                    new Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult, MessageBoxOptions, MessageBoxResult>(MessageBox.Show),
                    new object[] { messageBoxText, caption, button, icon, defaultResult, options });
            }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Threading;

namespace TableCloth.Components;

public sealed class MessageBoxService(
    IApplicationService applicationService) : IMessageBoxService
{
    public MessageBoxResult Show(Window
#if !NETFX
?
#endif
        owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options = default)
    {
        if (owner == null)
            owner = applicationService.GetMainWindow();

        // owner 파라미터를 null 참조로 지정하더라도 Windows Forms 처럼 parent-less 메시지 박스를 만들어주지는 않음.
        // GH-121 fix
        if (owner != null)
        {
            return (MessageBoxResult)applicationService.DispatchInvoke(
                new Func<Window, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult, MessageBoxOptions, MessageBoxResult>(MessageBox.Show),
                new object[] { owner, messageBoxText, caption, button, icon, defaultResult, options });
        }
        else
        {
            return (MessageBoxResult)applicationService.DispatchInvoke(
                new Func<string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult, MessageBoxOptions, MessageBoxResult>(MessageBox.Show),
                new object[] { messageBoxText, caption, button, icon, defaultResult, options });
        }
    }
}

using System.Windows;

namespace TableCloth.Components;

public sealed class MessageBoxService(
    Application application) : IMessageBoxService
{
    // owner 파라미터를 null 참조로 지정하더라도 Windows Forms 처럼 parent-less 메시지 박스를 만들어주지는 않음.
    // GH-121 fix
    public MessageBoxResult Show(Window
#if !NETFX
?
#endif
        owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options = default)
    {
        if (owner == null)
            owner = application.MainWindow;

        if (owner != null)
            return MessageBox.Show(owner, messageBoxText, caption, button, icon, defaultResult, options);
        else
            return MessageBox.Show(messageBoxText, caption, button, icon, defaultResult, options);
    }
}

using Avalonia.Controls;
using TableCloth.Models;

namespace TableCloth.Components;

// 이슈 #296: WPF MessageBox 계약(System.Windows 열거형) → UI 중립 열거형 + Avalonia Window.
public interface IMessageBoxService
{
    AppMessageBoxResult Show(Window? owner, string messageBoxText, string caption,
        AppMessageBoxButton button, AppMessageBoxImage icon, AppMessageBoxResult defaultResult);
}

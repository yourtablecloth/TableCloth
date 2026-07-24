using Avalonia.Controls;
using System;
using TableCloth.Dialogs;
using TableCloth.Models;

namespace TableCloth.Components.Implementations;

// 이슈 #296: WPF MessageBox.Show → 자작 MessageBoxWindow(동기 모달, Dispatcher.PushFrame). 항상 UI 스레드에서 실행.
public sealed class MessageBoxService(
    IApplicationService applicationService) : IMessageBoxService
{
    public AppMessageBoxResult Show(Window? owner, string messageBoxText, string caption,
        AppMessageBoxButton button, AppMessageBoxImage icon, AppMessageBoxResult defaultResult)
    {
        var result = applicationService.DispatchInvoke(new Func<AppMessageBoxResult>(() =>
        {
            var resolvedOwner = owner ?? applicationService.GetMainWindow();
            return MessageBoxWindow.ShowModal(resolvedOwner, messageBoxText, caption, button, icon, defaultResult);
        }), []);

        return result is AppMessageBoxResult r ? r : defaultResult;
    }
}

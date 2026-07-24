using Avalonia.Controls;
using Spork.Dialogs;
using System;
using TableCloth.Models;

namespace Spork.Components.Implementations
{
    // 이슈 #296: WPF MessageBox.Show → 자작 MessageBoxWindow(동기 모달, Dispatcher.PushFrame). 항상 UI 스레드에서 실행.
    public sealed class MessageBoxService : IMessageBoxService
    {
        public MessageBoxService(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        private readonly IApplicationService _applicationService;

        public AppMessageBoxResult Show(Window? owner, string messageBoxText, string caption,
            AppMessageBoxButton button, AppMessageBoxImage icon, AppMessageBoxResult defaultResult)
        {
            var result = _applicationService.DispatchInvoke(new Func<AppMessageBoxResult>(() =>
            {
                var resolvedOwner = owner ?? _applicationService.GetMainWindow();
                return MessageBoxWindow.ShowModal(resolvedOwner, messageBoxText, caption, button, icon, defaultResult);
            }), Array.Empty<object>());

            return result is AppMessageBoxResult r ? r : defaultResult;
        }
    }
}

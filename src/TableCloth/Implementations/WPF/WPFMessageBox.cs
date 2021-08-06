using System;
using System.Windows;
using System.Windows.Threading;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations.WPF
{
    public sealed class WPFMessageBox : IAppMessageBox
    {
        public void DisplayInfo(object parentWindowHandle, string message)
            => InvokeViaUIThread(
                parentWindowHandle is Window window ? window.Dispatcher : Dispatcher.CurrentDispatcher,
                () => MessageBox.Show(
                    (parentWindowHandle is Window window ? window : null),
                    message, StringResources.TitleText_Info,
                    MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK)
                );

        public void DisplayError(object parentWindowHandle, Exception failureReason, bool isCritical)
            => DisplayError(parentWindowHandle, failureReason is AggregateException ? failureReason.InnerException.Message : failureReason.Message, isCritical);

        public void DisplayError(object parentWindowHandle, string message, bool isCritical)
            => InvokeViaUIThread(
                parentWindowHandle is Window window ? window.Dispatcher : Dispatcher.CurrentDispatcher,
                () => MessageBox.Show(
                    (parentWindowHandle is Window window ? window : null),
                    message, (isCritical ? StringResources.TitleText_Error : StringResources.TitleText_Warning),
                    MessageBoxButton.OK, (isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning), MessageBoxResult.OK)
                );

        private MessageBoxResult InvokeViaUIThread(Dispatcher dispatcher, Func<MessageBoxResult> func)
            => (MessageBoxResult)dispatcher.Invoke(func, Array.Empty<object>());
    }
}

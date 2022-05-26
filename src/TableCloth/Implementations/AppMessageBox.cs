using System;
using System.Windows;
using System.Windows.Threading;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class AppMessageBox : IAppMessageBox
    {
        public MessageBoxResult DisplayInfo(object parentWindowHandle, string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK)
        {
            var dispatcher = parentWindowHandle is Window window ? window?.Dispatcher : null;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            return (MessageBoxResult)dispatcher.Invoke(
                new Func<object, string, MessageBoxResult>((parent, message) =>
                {
                    return parent is Window window
                        ? MessageBox.Show(
                            window, message, StringResources.TitleText_Info,
                            messageBoxButton, MessageBoxImage.Information, MessageBoxResult.OK)
                        : MessageBox.Show(
                            message, StringResources.TitleText_Info,
                            messageBoxButton, MessageBoxImage.Information, MessageBoxResult.OK);
                }),
                new object[] { parentWindowHandle, message, });
        }

        public int DisplayError(object parentWindowHandle, Exception failureReason, bool isCritical)
        {
            var unwrappedException = failureReason;

            if (failureReason is AggregateException ae)
                unwrappedException = ae.InnerException;

            return DisplayError(parentWindowHandle, unwrappedException?.Message ?? StringResources.UnknownText, isCritical);
        }

        public int DisplayError(object parentWindowHandle, string message, bool isCritical)
        {
            var dispatcher = parentWindowHandle is Window window ? window?.Dispatcher : null;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            return (int)dispatcher.Invoke(
                new Func<object, string, bool, int>((parent, message, isCritical) =>
                {
                    return parent is Window window
                        ? (int)MessageBox.Show(
                            window, message,
                            isCritical ? StringResources.TitleText_Error : StringResources.TitleText_Warning,
                            MessageBoxButton.OK,
                            isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning, MessageBoxResult.OK)
                        : (int)MessageBox.Show(
                            message,
                            isCritical ? StringResources.TitleText_Error : StringResources.TitleText_Warning,
                            MessageBoxButton.OK,
                            isCritical ? MessageBoxImage.Stop : MessageBoxImage.Warning, MessageBoxResult.OK);
                }),
                new object[] { parentWindowHandle, message, isCritical });
        }
    }
}

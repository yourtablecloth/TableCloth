using System;
using System.Windows;
using System.Windows.Threading;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class AppMessageBox : IAppMessageBox
    {
        public int DisplayInfo(object parentWindowHandle, string message)
        {
            var dispatcher = parentWindowHandle is Window window ? window?.Dispatcher : null;

            if (dispatcher == null)
                dispatcher = Dispatcher.CurrentDispatcher;

            return (int)dispatcher.Invoke(
                new Func<object, string, int>((parent, message) =>
                {
                    return parent is Window window
                        ? (int)MessageBox.Show(
                            window, message, StringResources.TitleText_Info,
                            MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK)
                        : (int)MessageBox.Show(
                            message, StringResources.TitleText_Info,
                            MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
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

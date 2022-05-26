using System;
using System.Windows;

namespace TableCloth.Contracts
{
    public interface IAppMessageBox
    {
        MessageBoxResult DisplayInfo(object parentWindowHandle, string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK);

        int DisplayError(object parentWindowHandle, Exception failureReason, bool isCritical);

        int DisplayError(object parentWindowHandle, string message, bool isCritical);
    }
}

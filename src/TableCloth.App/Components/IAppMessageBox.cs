using System;
using System.Runtime.CompilerServices;
using System.Windows;

namespace TableCloth.Components;

public interface IAppMessageBox
{
    MessageBoxResult DisplayError(Exception? failureReason, bool isCritical,
        [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0);
    MessageBoxResult DisplayError(string? message, bool isCritical,
        [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0);
    MessageBoxResult DisplayInfo(string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK);
}
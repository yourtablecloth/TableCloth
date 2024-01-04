using System;
using System.Windows;

namespace TableCloth.Components;

public interface IAppMessageBox
{
    MessageBoxResult DisplayError(Exception? failureReason, bool isCritical);
    MessageBoxResult DisplayError(string? message, bool isCritical);
    MessageBoxResult DisplayInfo(string message, MessageBoxButton messageBoxButton = MessageBoxButton.OK);
}
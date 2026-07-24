using System;
using System.Runtime.CompilerServices;
using TableCloth.Models;

namespace Spork.Components
{
    public interface IAppMessageBox
    {
        AppMessageBoxResult DisplayError(Exception failureReason, bool isCritical,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0);
        AppMessageBoxResult DisplayError(string message, bool isCritical,
            [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0);
        AppMessageBoxResult DisplayInfo(string message, AppMessageBoxButton messageBoxButton = AppMessageBoxButton.OK);
        AppMessageBoxResult DisplayQuestion(string message, AppMessageBoxButton messageBoxButton = AppMessageBoxButton.YesNo, AppMessageBoxResult defaultAnswer = AppMessageBoxResult.Yes);
    }
}

using System;

namespace TableCloth.Contracts
{
    public interface IAppMessageBox
    {
        void DisplayInfo(object parentWindowHandle, string message);

        void DisplayError(object parentWindowHandle, Exception failureReason, bool isCritical);

        void DisplayError(object parentWindowHandle, string message, bool isCritical);
    }
}

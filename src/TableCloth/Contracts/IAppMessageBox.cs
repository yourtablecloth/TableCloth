using System;

namespace TableCloth.Contracts
{
    public interface IAppMessageBox
    {
        int DisplayInfo(object parentWindowHandle, string message);

        int DisplayError(object parentWindowHandle, Exception failureReason, bool isCritical);

        int DisplayError(object parentWindowHandle, string message, bool isCritical);
    }
}

using System;

namespace TableCloth.Contracts
{
    public interface IAppMessageBox
    {
        void DisplayInfo(string message);

        void DisplayError(Exception failureReason, bool isCritical);

        void DisplayError(string message, bool isCritical);
    }
}

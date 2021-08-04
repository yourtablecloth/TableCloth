using System;
using System.Collections.Generic;

namespace TableCloth.Contracts
{
    public interface IAppUserInterface
    {
        void DisplayError(IEnumerable<string> args, Exception failureReason, bool isCritical);

        void StartApplication(IEnumerable<string> args);
    }
}

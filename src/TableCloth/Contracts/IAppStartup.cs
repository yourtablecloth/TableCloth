using System;
using System.Collections.Generic;

namespace TableCloth.Contracts
{
    public interface IAppStartup
    {
        IEnumerable<string> Arguments { get; set; }

        string AppDataDirectoryPath { get; }

        bool HasRequirementsMet(out Exception failedResaon, out bool isCritical);

        bool Initialize(out Exception failedReason, out bool isCritical);
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace TableCloth.Components;

public sealed class SandboxCleanupManager
{
    private readonly List<string> _temporaryDirectories = new List<string>();

    public string? CurrentDirectory { get; private set; }

    public void SetWorkingDirectory(string workingDirectory)
    {
        var normalizedPath = Path.GetFullPath(workingDirectory);

        if (!Directory.Exists(normalizedPath))
            throw new DirectoryNotFoundException($"Directory not found: {normalizedPath}");

        CurrentDirectory = normalizedPath;

        if (!_temporaryDirectories.Contains(CurrentDirectory))
            _temporaryDirectories.Add(CurrentDirectory);
    }

    public void TryCleanup()
    {
        foreach (var eachDirectory in _temporaryDirectories)
        {
            if (!string.IsNullOrWhiteSpace(CurrentDirectory))
            {
                if (string.Equals(Path.GetFullPath(eachDirectory), Path.GetFullPath(CurrentDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    if (Helpers.GetSandboxRunningState())
                    {
                        Helpers.OpenExplorer(eachDirectory);
                        continue;
                    }
                }
            }

            if (!Directory.Exists(eachDirectory))
                continue;

            try { Directory.Delete(eachDirectory, true); }
            catch { Helpers.OpenExplorer(eachDirectory); }
        }
    }
}

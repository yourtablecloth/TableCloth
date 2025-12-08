using System;
using System.IO;

namespace TableCloth.Components.Implementations;

public sealed class SandboxCleanupManager : ISandboxCleanupManager
{
    public string? CurrentDirectory { get; private set; }

    public void SetWorkingDirectory(string workingDirectory)
    {
        var normalizedPath = Path.GetFullPath(workingDirectory);

        if (!Directory.Exists(normalizedPath))
            TableClothAppException.Throw($"Directory not found: {normalizedPath}");

        CurrentDirectory = normalizedPath;
    }

    public void TryCleanup()
    {
        if (string.IsNullOrWhiteSpace(CurrentDirectory))
            return;

        if (!Directory.Exists(CurrentDirectory))
            return;

        try { Directory.Delete(CurrentDirectory, true); }
        catch { /* 다음 실행 시 덮어쓰므로 무시 */ }
    }
}

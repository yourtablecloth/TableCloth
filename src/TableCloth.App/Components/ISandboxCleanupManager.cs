namespace TableCloth.Components;

public interface ISandboxCleanupManager
{
    string? CurrentDirectory { get; }

    void SetWorkingDirectory(string workingDirectory);
    void TryCleanup();
}
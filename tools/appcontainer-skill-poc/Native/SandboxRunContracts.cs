namespace TableCloth.AppContainerPoc;

public sealed record SandboxRunRequest(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    string Stdin,
    IReadOnlyList<string> ReadOnlyDirectories,
    IReadOnlyList<string> ReadWriteDirectories,
    bool AllowNetwork = false,
    int TimeoutMilliseconds = 15000,
    long MemoryLimitBytes = 268435456,
    int MaxOutputBytes = 65536,
    int MaxProcesses = 1);

public sealed record SandboxRunResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    bool TimedOut,
    bool OutputLimited,
    bool WasAppContainer,
    string? Failure = null);

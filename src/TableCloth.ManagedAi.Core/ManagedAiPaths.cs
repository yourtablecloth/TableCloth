namespace TableCloth.ManagedAi;

public sealed class ManagedAiPaths
{
    public ManagedAiPaths(string root)
    {
        if (!Path.IsPathFullyQualified(root) || root.StartsWith("\\\\", StringComparison.Ordinal)) throw Invalid();
        Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if (Root.Length > 140) throw Invalid();
        RejectReparsePoints(Root);
    }

    public static ManagedAiPaths ForCurrentUser() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TableCloth", "ManagedAi"));

    public string Root { get; }
    public string RuntimeRoot => Under("runtimes", "openai-codex");
    public string Releases => Under("runtimes", "openai-codex", "releases");
    public string State => Under("runtimes", "openai-codex", "state");
    public string Profile => Under("profiles", "openai-codex", "codex-home");
    public string Runs => Under("runs");
    public string Downloads => Under("cache", "downloads");
    public string Logs => Under("logs");
    public string Current => Under("runtimes", "openai-codex", "state", "current.json");
    public string Previous => Under("runtimes", "openai-codex", "state", "previous.json");

    public string Under(params string[] components)
    {
        var path = Path.GetFullPath(Path.Combine([Root, .. components]));
        if (path.Length > 240 || !path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw Invalid();
        RejectReparsePoints(path);
        return path;
    }

    public IDisposable AcquireOperation()
    {
        Directory.CreateDirectory(RuntimeRoot);
        try
        {
            return new FileStream(Under("runtimes", "openai-codex", "install.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException) { throw new ManagedAiException(AiFailureCode.RuntimeBusy); }
    }

    public static void RejectReparsePoints(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            if ((File.Exists(current.FullName) || Directory.Exists(current.FullName)) &&
                (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0) throw Invalid();
        }
    }

    // Call only for a unique directory created by the current operation, never for an arbitrary stale path.
    public void DeleteOwnedDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        RejectReparsePoints(full);
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
    private static ManagedAiException Invalid() => new(AiFailureCode.InvalidPath);
}

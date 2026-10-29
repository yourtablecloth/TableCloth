using System.Formats.Tar;
using System.IO.Compression;

namespace TableCloth.ManagedAi.OpenAi;

public static class SafeCodexArchive
{
    public static readonly string[] RequiredFiles = ["codex-package.json", "bin/codex.exe",
        "bin/codex-code-mode-host.exe", "codex-path/rg.exe", "codex-resources/codex-command-runner.exe",
        "codex-resources/codex-windows-sandbox-setup.exe"];

    public static async Task ExtractAsync(Stream archive, string staging, ManagedAiOptions options, CancellationToken token)
    {
        var root = Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        ManagedAiPaths.RejectReparsePoints(staging);
        using var gzip = new GZipStream(archive, CompressionMode.Decompress, leaveOpen: true);
        using var reader = new TarReader(gzip, leaveOpen: true);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        int count = 0;
        TarEntry? entry;
        try
        {
            while ((entry = await reader.GetNextEntryAsync(false, token)) is not null)
            {
                if (++count > options.MaxArchiveEntries || entry.Length < 0 ||
                    entry.Length > options.MaxExtractedBytes - total) throw Rejected();
                total += entry.Length;
                if (entry.EntryType is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                    throw Rejected();
                var name = entry.Name.Replace('\\', '/');
                while (name.StartsWith("./", StringComparison.Ordinal)) name = name[2..];
                if (entry.EntryType == TarEntryType.Directory && name is "" or ".") continue;
                name = name.TrimEnd('/');
                if (name.StartsWith('/') || name.Split('/').Any(x => x.Length == 0 || x is ".." or "." ||
                    x.EndsWith('.') || x.EndsWith(' ') || x.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    IsDeviceName(x))) throw Rejected();
                var target = Path.GetFullPath(Path.Combine(root, name));
                if (target.Length > 240 || !target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !seen.Add(target))
                    throw Rejected();
                ManagedAiPaths.RejectReparsePoints(target);
                if (entry.EntryType == TarEntryType.Directory) { Directory.CreateDirectory(target); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                if (entry.DataStream is not null) await entry.DataStream.CopyToAsync(output, token);
                if (output.Length != entry.Length) throw Rejected();
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException) { throw Rejected(); }
    }

    public static void ValidateLayout(string root)
    {
        foreach (var relative in RequiredFiles)
        {
            var path = Path.Combine(root, relative);
            ManagedAiPaths.RejectReparsePoints(path);
            if (!File.Exists(path) || new FileInfo(path).Length == 0) throw Rejected();
        }
    }
    private static bool IsDeviceName(string segment)
    {
        var stem = segment.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '0' and <= '9');
    }
    private static ManagedAiException Rejected() => new(AiFailureCode.ArchiveRejected);
}

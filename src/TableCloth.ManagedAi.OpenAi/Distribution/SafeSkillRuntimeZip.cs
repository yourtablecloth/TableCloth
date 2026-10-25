using System.IO.Compression;
using System.Buffers.Binary;

namespace TableCloth.ManagedAi.OpenAi;

public sealed record SkillRuntimeArchiveLimits
{
    public long MaxDownloadBytes { get; init; } = 128L * 1024 * 1024;
    public long MaxExtractedBytes { get; init; } = 256L * 1024 * 1024;
    public long MaxFileBytes { get; init; } = 128L * 1024 * 1024;
    public int MaxEntries { get; init; } = 8000;
    public int MaxCompressionRatio { get; init; } = 200;
}

public static class SafeSkillRuntimeZip
{
    internal const int MaxTargetPathLength = 4096;
    public static async Task ExtractAsync(Stream archive, string destination, string prefix,
        SkillRuntimeArchiveLimits limits, CancellationToken token)
    {
        var root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        ManagedAiPaths.RejectReparsePoints(destination);
        try
        {
            ValidateCentralDirectory(archive, limits);
            archive.Position = 0;
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > limits.MaxEntries) throw Rejected();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var spellings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();
                var mode = (entry.ExternalAttributes >> 16) & 0xf000;
                if (mode is not (0 or 0x8000 or 0x4000) ||
                    (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0) throw Rejected();
                var name = entry.FullName;
                if (name.Contains('\\')) throw Rejected();
                var directory = name.EndsWith('/');
                name = name.TrimEnd('/');
                if (prefix.Length != 0)
                {
                    if (name == prefix && directory) continue;
                    if (!name.StartsWith(prefix + "/", StringComparison.Ordinal)) throw Rejected();
                    name = name[(prefix.Length + 1)..];
                }
                if (name.Length == 0 || name.StartsWith('/') || name.Split('/').Any(InvalidSegment)) throw Rejected();
                var spelling = "";
                foreach (var segment in name.Split('/'))
                {
                    spelling = spelling.Length == 0 ? segment : spelling + "/" + segment;
                    if (spellings.TryGetValue(spelling, out var prior) && prior != spelling) throw Rejected();
                    spellings[spelling] = spelling;
                }
                var target = Path.GetFullPath(Path.Combine(destination, name));
                if (target.Length > MaxTargetPathLength || !target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !seen.Add(target)) throw Rejected();
                ManagedAiPaths.RejectReparsePoints(target);
                if (entry.Length < 0 || entry.Length > limits.MaxFileBytes || entry.Length > limits.MaxExtractedBytes - total ||
                    entry.Length > 0 && (entry.CompressedLength == 0 || entry.Length / entry.CompressedLength > limits.MaxCompressionRatio)) throw Rejected();
                total += entry.Length;
                if (directory)
                {
                    if (entry.Length != 0 || mode == 0x8000) throw Rejected();
                    Directory.CreateDirectory(target);
                    continue;
                }
                if (mode == 0x4000) throw Rejected();
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = entry.Open();
                await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                long written = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, token)) != 0)
                {
                    written += count;
                    if (written > entry.Length || written > limits.MaxFileBytes) throw Rejected();
                    await output.WriteAsync(buffer.AsMemory(0, count), token);
                }
                if (written != entry.Length) throw Rejected();
            }
        }
        catch (PathTooLongException)
        { throw new ManagedAiException(AiFailureCode.PathLengthUnsupported); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException)
        { throw Rejected(); }
    }

    // Bound central-directory entries before ZipArchive allocates its complete entry collection.
    // These pinned Windows distributions fit ordinary single-volume ZIP, so ZIP64 is unnecessary.
    private static void ValidateCentralDirectory(Stream archive, SkillRuntimeArchiveLimits limits)
    {
        if (!archive.CanSeek || !archive.CanRead || archive.Length < 22 || archive.Length > limits.MaxDownloadBytes) throw Rejected();
        var tail = new byte[(int)Math.Min(archive.Length, 65557)];
        archive.Position = archive.Length - tail.Length;
        archive.ReadExactly(tail);
        var end = -1;
        for (var index = tail.Length - 22; index >= 0; index--)
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(index, 4)) == 0x06054b50 &&
                index + 22 + BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(index + 20, 2)) == tail.Length)
            { end = index; break; }
        if (end < 0) throw Rejected();
        var header = tail.AsSpan(end, 22);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(header[10..]);
        var size = BinaryPrimitives.ReadUInt32LittleEndian(header[12..]);
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
        if (BinaryPrimitives.ReadUInt16LittleEndian(header[4..]) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(header[6..]) != 0 ||
            BinaryPrimitives.ReadUInt16LittleEndian(header[8..]) != count || count == ushort.MaxValue || count > limits.MaxEntries ||
            (long)offset + size != archive.Length - tail.Length + end) throw Rejected();
        archive.Position = offset;
        Span<byte> entry = stackalloc byte[46];
        long total = 0;
        for (var index = 0; index < count; index++)
        {
            archive.ReadExactly(entry);
            if (BinaryPrimitives.ReadUInt32LittleEndian(entry) != 0x02014b50) throw Rejected();
            var compressed = BinaryPrimitives.ReadUInt32LittleEndian(entry[20..]);
            var expanded = BinaryPrimitives.ReadUInt32LittleEndian(entry[24..]);
            var nameBytes = BinaryPrimitives.ReadUInt16LittleEndian(entry[28..]);
            var extraBytes = BinaryPrimitives.ReadUInt16LittleEndian(entry[30..]);
            var commentBytes = BinaryPrimitives.ReadUInt16LittleEndian(entry[32..]);
            if (compressed == uint.MaxValue || expanded == uint.MaxValue || expanded > limits.MaxFileBytes ||
                expanded > limits.MaxExtractedBytes - total || nameBytes is 0 or > 1024 ||
                expanded > 0 && (compressed == 0 || expanded / compressed > limits.MaxCompressionRatio) ||
                BinaryPrimitives.ReadUInt32LittleEndian(entry[42..]) >= offset) throw Rejected();
            total += expanded;
            var next = archive.Position + nameBytes + extraBytes + commentBytes;
            if (next > (long)offset + size) throw Rejected();
            archive.Position = next;
        }
        if (archive.Position != (long)offset + size) throw Rejected();
    }

    private static bool InvalidSegment(string segment)
    {
        if (segment.Length is 0 or > 150 || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ') ||
            segment.Any(c => char.IsControl(c) || "<>:\"|?*".Contains(c))) return true;
        var stem = segment.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || stem.Length == 4 &&
            (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
            stem[3] is >= '0' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3';
    }

    private static ManagedAiException Rejected() => new(AiFailureCode.ArchiveRejected);
}

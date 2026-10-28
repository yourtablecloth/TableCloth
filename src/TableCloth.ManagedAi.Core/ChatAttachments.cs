using System.Text;

namespace TableCloth.ManagedAi;

public sealed class AiChatAttachment
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".csv", ".tsv", ".json", ".jsonl", ".log", ".xml", ".yaml", ".yml",
        ".cs", ".py", ".js", ".mjs", ".cjs", ".ts", ".tsx", ".jsx", ".html", ".htm", ".css", ".scss",
        ".ini", ".cfg", ".conf", ".config", ".toml", ".sql", ".sh", ".ps1", ".psm1", ".bat", ".cmd",
        ".rst", ".r", ".go", ".rs", ".c", ".h", ".cpp", ".hpp", ".java", ".kt", ".swift", ".vb", ".xaml"
    };
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pfx", ".p12", ".pem", ".key", ".der", ".cer", ".crt", ".p7b", ".p7c", ".p7s", ".p8",
        ".pkcs8", ".p10", ".csr", ".crl", ".jks", ".keystore", ".kdb", ".ppk"
    };
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly byte[] _content;

    public string FileName { get; }
    public string MediaType { get; }
    public bool IsImage { get; }
    public long Length => _content.LongLength;
    public string? Text { get; }

    private AiChatAttachment(string fileName, byte[] content, string mediaType, bool isImage, string? text)
    {
        FileName = fileName;
        _content = content;
        MediaType = mediaType;
        IsImage = isImage;
        Text = text;
    }

    public byte[] GetContent() => (byte[])_content.Clone();

    public static AiChatAttachment Create(string fileName, byte[] content)
    {
        ValidateFileName(fileName);
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (BlockedExtensions.Contains(extension) || stem.Equals("id_rsa", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("id_dsa", StringComparison.OrdinalIgnoreCase) || stem.Equals("id_ecdsa", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("id_ed25519", StringComparison.OrdinalIgnoreCase) || stem.Equals("signPri", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("signCert", StringComparison.OrdinalIgnoreCase))
            throw Failure(AiFailureCode.AttachmentBlocked);
        if (content is null || content.Length == 0) throw Failure(AiFailureCode.AttachmentInvalid);
        if (content.Length > AiChatAttachmentPolicy.MaximumFileBytes) throw Failure(AiFailureCode.AttachmentLimitExceeded);
        var copy = (byte[])content.Clone();
        BlockKeyMaterial(Encoding.Latin1.GetString(copy));
        fileName = SensitiveDataSanitizer.Sanitize(stem) + extension;
        ValidateFileName(fileName);

        if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            ValidatePng(copy);
            return new(fileName, copy, "image/png", true, null);
        }
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            ValidateJpeg(copy);
            return new(fileName, copy, "image/jpeg", true, null);
        }
        if (!TextExtensions.Contains(extension)) throw Failure(AiFailureCode.AttachmentInvalid);
        if (copy.Length > AiChatAttachmentPolicy.MaximumTextBytes) throw Failure(AiFailureCode.AttachmentLimitExceeded);

        string text;
        try
        {
            if (copy.Length >= 2 && copy[0] == 0xff && copy[1] == 0xfe)
                text = new UnicodeEncoding(false, false, true).GetString(copy, 2, copy.Length - 2);
            else if (copy.Length >= 2 && copy[0] == 0xfe && copy[1] == 0xff)
                text = new UnicodeEncoding(true, false, true).GetString(copy, 2, copy.Length - 2);
            else
            {
                var offset = copy.Length >= 3 && copy[0] == 0xef && copy[1] == 0xbb && copy[2] == 0xbf ? 3 : 0;
                text = Utf8.GetString(copy, offset, copy.Length - offset);
            }
        }
        catch (DecoderFallbackException) { throw Failure(AiFailureCode.AttachmentInvalid); }
        if (text.Length == 0 || text.Any(c => char.IsControl(c) && c is not '\t' and not '\r' and not '\n'))
            throw Failure(AiFailureCode.AttachmentInvalid);
        BlockKeyMaterial(text);
        if (text.Length > AiChatAttachmentPolicy.MaximumTextCharacters) throw Failure(AiFailureCode.AttachmentLimitExceeded);
        text = SensitiveDataSanitizer.Sanitize(text);
        copy = Utf8.GetBytes(text);
        if (text.Length > AiChatAttachmentPolicy.MaximumTextCharacters || copy.Length > AiChatAttachmentPolicy.MaximumTextBytes)
            throw Failure(AiFailureCode.AttachmentLimitExceeded);
        return new(fileName, copy, "text/plain", false, text);
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 128 || fileName != fileName.Trim() ||
            fileName is "." or ".." || fileName.EndsWith('.') ||
            fileName.Any(c => char.IsControl(c) || c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|'))
            throw Failure(AiFailureCode.AttachmentInvalid);
    }

    private static void BlockKeyMaterial(string text)
    {
        if (text.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase) && text.Contains("-----BEGIN", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-----BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-----BEGIN TRUSTED CERTIFICATE", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-----BEGIN X509", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-----BEGIN PKCS", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("openssh-key-v1", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("PuTTY-User-Key-File-", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("<RSAKeyValue", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("<ECKeyValue", StringComparison.OrdinalIgnoreCase))
            throw Failure(AiFailureCode.AttachmentBlocked);
    }

    private static void ValidatePng(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (bytes.Length < 58 || !bytes.AsSpan(0, 8).SequenceEqual(signature)) throw Failure(AiFailureCode.AttachmentInvalid);
        var offset = 8;
        var header = false;
        var imageData = false;
        var palette = false;
        var indexed = false;
        var dataEnded = false;
        while (offset <= bytes.Length - 12)
        {
            var length = ReadBigEndian(bytes, offset);
            if (length > int.MaxValue || length > bytes.Length - offset - 12) throw Failure(AiFailureCode.AttachmentInvalid);
            var count = (int)length;
            var type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
            if (type.Any(c => !char.IsAsciiLetter(c)) || char.IsLower(type[2]) ||
                ComputeCrc(bytes.AsSpan(offset + 4, count + 4)) != ReadBigEndian(bytes, offset + 8 + count))
                throw Failure(AiFailureCode.AttachmentInvalid);
            var start = offset + 8;
            if (!header)
            {
                if (type != "IHDR" || count != 13) throw Failure(AiFailureCode.AttachmentInvalid);
                ValidateDimensions(ReadBigEndian(bytes, start), ReadBigEndian(bytes, start + 4));
                var depth = bytes[start + 8];
                var color = bytes[start + 9];
                var validDepth = color switch
                {
                    0 => depth is 1 or 2 or 4 or 8 or 16,
                    2 or 4 or 6 => depth is 8 or 16,
                    3 => depth is 1 or 2 or 4 or 8,
                    _ => false
                };
                if (!validDepth || bytes[start + 10] != 0 || bytes[start + 11] != 0 || bytes[start + 12] > 1)
                    throw Failure(AiFailureCode.AttachmentInvalid);
                indexed = color == 3;
                header = true;
            }
            else if (type == "IHDR") throw Failure(AiFailureCode.AttachmentInvalid);
            else if (type == "PLTE")
            {
                if (palette || imageData || count == 0 || count > 768 || count % 3 != 0) throw Failure(AiFailureCode.AttachmentInvalid);
                palette = true;
            }
            else if (type == "IDAT")
            {
                if (dataEnded || indexed && !palette) throw Failure(AiFailureCode.AttachmentInvalid);
                if (count > 0) imageData = true;
            }
            else if (type == "IEND")
            {
                if (count != 0 || !imageData || offset + 12 != bytes.Length) throw Failure(AiFailureCode.AttachmentInvalid);
                return;
            }
            else
            {
                if (char.IsUpper(type[0])) throw Failure(AiFailureCode.AttachmentInvalid);
                if (imageData) dataEnded = true;
            }
            offset += count + 12;
        }
        throw Failure(AiFailureCode.AttachmentInvalid);
    }

    private static void ValidateJpeg(byte[] bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8) throw Failure(AiFailureCode.AttachmentInvalid);
        var offset = 2;
        var frame = false;
        var scan = false;
        var entropy = false;
        while (offset < bytes.Length)
        {
            if (bytes[offset++] != 0xff) throw Failure(AiFailureCode.AttachmentInvalid);
            while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
            if (offset >= bytes.Length) throw Failure(AiFailureCode.AttachmentInvalid);
            var marker = bytes[offset++];
            if (marker == 0xd9)
            {
                if (!frame || !scan || !entropy || offset != bytes.Length) throw Failure(AiFailureCode.AttachmentInvalid);
                return;
            }
            if (offset > bytes.Length - 2) throw Failure(AiFailureCode.AttachmentInvalid);
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            if (length < 2 || length > bytes.Length - offset) throw Failure(AiFailureCode.AttachmentInvalid);
            if (marker is 0xc0 or 0xc1 or 0xc2)
            {
                if (frame || length < 11 || bytes[offset + 2] != 8 || bytes[offset + 7] is < 1 or > 4 ||
                    length != 8 + 3 * bytes[offset + 7]) throw Failure(AiFailureCode.AttachmentInvalid);
                ValidateDimensions((uint)((bytes[offset + 5] << 8) | bytes[offset + 6]),
                    (uint)((bytes[offset + 3] << 8) | bytes[offset + 4]));
                frame = true;
            }
            else if (marker == 0xda)
            {
                if (!frame || length < 8 || bytes[offset + 2] is < 1 or > 4 || length != 6 + 2 * bytes[offset + 2])
                    throw Failure(AiFailureCode.AttachmentInvalid);
                scan = true;
            }
            else if (marker is not (0xc4 or 0xdb or 0xdd or 0xfe) && marker is not (>= 0xe0 and <= 0xef))
                throw Failure(AiFailureCode.AttachmentInvalid);
            offset += length;
            if (marker != 0xda) continue;
            while (offset < bytes.Length)
            {
                if (bytes[offset] != 0xff) { entropy = true; offset++; continue; }
                var next = offset + 1;
                while (next < bytes.Length && bytes[next] == 0xff) next++;
                if (next >= bytes.Length) throw Failure(AiFailureCode.AttachmentInvalid);
                if (bytes[next] == 0) { entropy = true; offset = next + 1; continue; }
                if (bytes[next] is >= 0xd0 and <= 0xd7) { offset = next + 1; continue; }
                break;
            }
        }
        throw Failure(AiFailureCode.AttachmentInvalid);
    }

    private static void ValidateDimensions(uint width, uint height)
    {
        if (width == 0 || height == 0) throw Failure(AiFailureCode.AttachmentInvalid);
        if (width > AiChatAttachmentPolicy.MaximumImageDimension || height > AiChatAttachmentPolicy.MaximumImageDimension ||
            (ulong)width * height > AiChatAttachmentPolicy.MaximumImagePixels)
            throw Failure(AiFailureCode.AttachmentLimitExceeded);
    }

    private static uint ReadBigEndian(byte[] bytes, int offset)
        => ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    private static uint ComputeCrc(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
        return ~crc;
    }

    private static ManagedAiException Failure(AiFailureCode code) => new(code);
}

public static class AiChatAttachmentPolicy
{
    public const int MaximumCount = 4;
    public const int MaximumFileBytes = 8 * 1024 * 1024;
    public const int MaximumTotalBytes = 16 * 1024 * 1024;
    public const int MaximumTextBytes = 64 * 1024;
    public const int MaximumTextCharacters = 12000;
    public const int MaximumTotalTextCharacters = 24000;
    public const int MaximumImagePixels = 16_777_216;
    public const int MaximumImageDimension = 8192;

    public static void Validate(IReadOnlyList<AiChatAttachment>? attachments)
    {
        if (attachments is null) return;
        if (attachments.Count > MaximumCount) throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
        long bytes = 0;
        var characters = 0;
        foreach (var attachment in attachments)
        {
            if (attachment is null) throw new ManagedAiException(AiFailureCode.AttachmentInvalid);
            bytes += attachment.Length;
            characters += attachment.Text?.Length ?? 0;
        }
        if (bytes > MaximumTotalBytes || characters > MaximumTotalTextCharacters)
            throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
    }
}

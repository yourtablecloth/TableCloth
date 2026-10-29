using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi;

internal sealed class ManagedAiPromptAttachment(AiChatAttachment attachment, Bitmap? preview, bool textMasked) : IDisposable
{
    public AiChatAttachment Attachment { get; } = attachment;
    public Bitmap? Preview { get; } = preview;
    public bool TextMasked { get; } = textMasked;
    public void Dispose() => Preview?.Dispose();
}

internal static class ManagedAiPromptAttachmentReader
{
    internal const int MaximumFileBytes = AiChatAttachmentPolicy.MaximumFileBytes;
    internal const int MaximumFiles = AiChatAttachmentPolicy.MaximumCount;

    public static async Task<IReadOnlyList<ManagedAiPromptAttachment>> ReadFilesAsync(
        IReadOnlyList<IStorageItem> files, CancellationToken token)
    {
        if (files.Count is 0 or > MaximumFiles) throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
        var result = new List<ManagedAiPromptAttachment>();
        try
        {
            foreach (var item in files)
            {
                token.ThrowIfCancellationRequested();
                if (item is not IStorageFile file) throw new ManagedAiException(AiFailureCode.AttachmentInvalid);
                var name = file.Name;
                if (CertificateInputGuard.ContainsProhibitedMaterial(name))
                    throw new ManagedAiException(AiFailureCode.AttachmentBlocked);
                await using var stream = await file.OpenReadAsync();
                var extension = Path.GetExtension(name);
                var maximum = extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? MaximumFileBytes : AiChatAttachmentPolicy.MaximumTextBytes;
                var bytes = await ReadBoundedAsync(stream, maximum, token);
                result.Add(await PrepareAsync(name, bytes, token));
            }
            AiChatAttachmentPolicy.Validate(result.Select(item => item.Attachment).ToArray());
            return result;
        }
        catch
        {
            foreach (var item in result) item.Dispose();
            throw;
        }
    }

    public static async Task<ManagedAiPromptAttachment> ReadBitmapAsync(Bitmap bitmap, CancellationToken token)
    {
        var size = bitmap.PixelSize;
        if (size.Width <= 0 || size.Height <= 0 || size.Width > AiChatAttachmentPolicy.MaximumImageDimension ||
            size.Height > AiChatAttachmentPolicy.MaximumImageDimension ||
            (long)size.Width * size.Height > AiChatAttachmentPolicy.MaximumImagePixels)
            throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
        var bytes = await Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            using var stream = new BoundedAttachmentStream(MaximumFileBytes);
            bitmap.Save(stream);
            return stream.ToArray();
        }, token);
        return await PrepareAsync("Clipboard-image.png", bytes, token);
    }

    private static Task<ManagedAiPromptAttachment> PrepareAsync(string name, byte[] bytes, CancellationToken token)
        => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            var attachment = AiChatAttachment.Create(name, bytes);
            if (CertificateInputGuard.ContainsProhibitedMaterial(attachment.FileName) ||
                (attachment.Text is { } text && (CertificateInputGuard.ContainsProhibitedMaterial(text) ||
                    CertificateInputGuard.ContainsProhibitedMaterial(DecodeOriginalText(bytes)))))
                throw new ManagedAiException(AiFailureCode.AttachmentBlocked);
            if (!attachment.IsImage)
                return new ManagedAiPromptAttachment(attachment, null,
                    !string.Equals(DecodeOriginalText(bytes), attachment.Text, StringComparison.Ordinal));
            using var stream = new MemoryStream(attachment.GetContent(), writable: false);
            using var image = new Bitmap(stream);
            if (image.PixelSize.Width <= 0 || image.PixelSize.Height <= 0 ||
                image.PixelSize.Width > AiChatAttachmentPolicy.MaximumImageDimension ||
                image.PixelSize.Height > AiChatAttachmentPolicy.MaximumImageDimension ||
                (long)image.PixelSize.Width * image.PixelSize.Height > AiChatAttachmentPolicy.MaximumImagePixels)
                throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
            var scale = Math.Min(1d, Math.Min(96d / image.PixelSize.Width, 96d / image.PixelSize.Height));
            var preview = image.CreateScaledBitmap(new PixelSize(Math.Max(1, (int)(image.PixelSize.Width * scale)),
                Math.Max(1, (int)(image.PixelSize.Height * scale))), BitmapInterpolationMode.MediumQuality);
            return new ManagedAiPromptAttachment(attachment, preview, false);
        }, token);

    private static string DecodeOriginalText(byte[] bytes)
    {
        if (bytes.Length >= 2 && bytes[0] == 0xff && bytes[1] == 0xfe)
            return new UnicodeEncoding(false, true, true).GetString(bytes, 2, bytes.Length - 2);
        if (bytes.Length >= 2 && bytes[0] == 0xfe && bytes[1] == 0xff)
            return new UnicodeEncoding(true, true, true).GetString(bytes, 2, bytes.Length - 2);
        var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream source, int maximum, CancellationToken token)
    {
        if (source.CanSeek && source.Length > maximum)
            throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
        using var destination = new BoundedAttachmentStream(maximum);
        var buffer = new byte[16 * 1024];
        int count;
        while ((count = await source.ReadAsync(buffer.AsMemory(), token)) > 0)
            destination.Write(buffer, 0, count);
        return destination.ToArray();
    }

    // Enforce the limit while an encoder or an unseekable storage stream writes.
    private sealed class BoundedAttachmentStream(int maximum) : MemoryStream
    {
        private void Check(int count)
        {
            if (Position + count > maximum) throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
        }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
        public override void SetLength(long value)
        {
            if (value > maximum) throw new ManagedAiException(AiFailureCode.AttachmentLimitExceeded);
            base.SetLength(value);
        }
    }
}

using System.Buffers.Binary;
using System.Text;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ChatAttachmentTests
{
    // A 1x1 PNG/JPEG encoded once with Windows GDI+, independent of the parser.
    private static byte[] Png() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAALSURBVBhXY2AAAgAABQABqtXIUQAAAABJRU5ErkJggg==");
    private static byte[] Jpeg() => Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD8qqKKKAP/2Q==");

    [TestMethod]
    public void ImageSnapshotsRemainImmutableWhenOriginalAndReturnedArraysChange()
    {
        var bytes = Png();
        var expected = (byte[])bytes.Clone();
        var attachment = AiChatAttachment.Create("screen.png", bytes);
        bytes[0] = 0;
        var exported = attachment.GetContent();
        exported[1] = 0;
        CollectionAssert.AreEqual(expected, attachment.GetContent());
        Assert.IsTrue(attachment.IsImage);
        Assert.AreEqual("image/png", attachment.MediaType);
        Assert.AreEqual((long)expected.Length, attachment.Length);
        Assert.IsNull(attachment.Text);
        var jpeg = AiChatAttachment.Create("photo.JPEG", Jpeg());
        Assert.AreEqual("image/jpeg", jpeg.MediaType);
        Assert.IsTrue(jpeg.IsImage);
    }

    [TestMethod]
    public void TextNormalizesStrictUnicodeAndPersistsOnlySanitizedUtf8AndFilename()
    {
        const string original = "연락처: 010-1234-5678\n주민등록번호: 900101-1234567";
        var expected = SensitiveDataSanitizer.Sanitize(original);
        foreach (var encoding in new Encoding[] { new UTF8Encoding(true), Encoding.Unicode, Encoding.BigEndianUnicode })
        {
            var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(original)).ToArray();
            var attachment = AiChatAttachment.Create("010-1234-5678.md", bytes);
            Assert.AreEqual("[전화번호].md", attachment.FileName);
            Assert.AreEqual(expected, attachment.Text);
            Assert.AreEqual(expected, new UTF8Encoding(false, true).GetString(attachment.GetContent()));
            Assert.IsFalse(attachment.IsImage);
            Assert.DoesNotContain("900101", attachment.Text!);
            Assert.DoesNotContain("010-1234", attachment.Text!);
        }
    }

    [TestMethod]
    [DataRow("..\\private.txt")]
    [DataRow("/private.txt")]
    [DataRow("C:private.txt")]
    [DataRow("draft\n.txt")]
    [DataRow("draft?.txt")]
    [DataRow(" draft.txt")]
    [DataRow("draft.txt.")]
    [DataRow("archive.zip")]
    [DataRow("report.pdf")]
    public void UnsafeNamesAndUnsupportedFormatsUseFixedErrors(string name)
    {
        var exception = Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create(name, Encoding.UTF8.GetBytes("text")));
        Assert.AreEqual(AiFailureCode.AttachmentInvalid, exception.Code);
        Assert.AreEqual("AttachmentInvalid", exception.Message);
        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    [DataRow("signCert.der", "text")]
    [DataRow("private.pfx", "text")]
    [DataRow("id_rsa.txt", "text")]
    [DataRow("notes.txt", "-----BEGIN ENCRYPTED PRIVATE KEY-----\nnot a real key")]
    [DataRow("notes.txt", "-----BEGIN EC PRIVATE KEY-----\nnot a real key")]
    [DataRow("notes.txt", "-----BEGIN OPENSSH PRIVATE KEY-----\nnot a real key")]
    [DataRow("notes.txt", "-----BEGIN CERTIFICATE-----\nnot a real certificate")]
    [DataRow("notes.txt", "PuTTY-User-Key-File-3: ssh-ed25519")]
    [DataRow("notes.xml", "<RSAKeyValue><D>not a real key</D></RSAKeyValue>")]
    public void CertificateAndPrivateKeyMaterialIsBlockedEvenWhenRenamed(string name, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var exception = Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create(name, bytes));
        Assert.AreEqual(AiFailureCode.AttachmentBlocked, exception.Code);
        var utf16 = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
        Assert.AreEqual(AiFailureCode.AttachmentBlocked,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create(name, utf16)).Code);
    }

    [TestMethod]
    public void InvalidTextEncodingBinaryControlsAndEmptyBodiesAreRejected()
    {
        foreach (var bytes in new byte[][] { [], [0xc0, 0xaf], [0xff, 0xfe, 0x41], [0x61, 0, 0x62],
            Encoding.UTF8.GetBytes("a\u0085b"), Encoding.UTF8.GetPreamble() })
            Assert.AreEqual(AiFailureCode.AttachmentInvalid,
                Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create("notes.txt", bytes)).Code);
        Assert.AreEqual(AiFailureCode.AttachmentInvalid,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create(new string('x', 129) + ".txt", [65])).Code);
    }

    [TestMethod]
    public void MalformedImagesCrcAndDimensionBombsAreRejectedBeforeDecoding()
    {
        var corrupt = Png();
        corrupt[16] ^= 1; // Invalid IHDR CRC.
        foreach (var bytes in new[] { corrupt, Png()[..33], Png().Concat(new byte[] { 0 }).ToArray(), Encoding.UTF8.GetBytes("not png") })
            Assert.AreEqual(AiFailureCode.AttachmentInvalid,
                Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create("bad.png", bytes)).Code);
        foreach (var dimensions in new[] { (8193u, 1u), (8192u, 8192u) })
        {
            var image = Png();
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(16), dimensions.Item1);
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(20), dimensions.Item2);
            BinaryPrimitives.WriteUInt32BigEndian(image.AsSpan(29), Crc(image.AsSpan(12, 17)));
            Assert.AreEqual(AiFailureCode.AttachmentLimitExceeded,
                Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create("huge.png", image)).Code);
        }
        var jpeg = Jpeg();
        Assert.AreEqual(AiFailureCode.AttachmentInvalid,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create("bad.jpg", jpeg[..^2])).Code);
        Assert.AreEqual(AiFailureCode.AttachmentInvalid,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create("bad.jpg", new byte[] { 0xff, 0xd8, 0xff, 0xd9 })).Code);
    }

    [TestMethod]
    public void PerFileCountAggregateBytesAndAggregateTextLimitsRejectWithoutTruncation()
    {
        Assert.AreEqual(AiFailureCode.AttachmentLimitExceeded,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create("big.png", new byte[AiChatAttachmentPolicy.MaximumFileBytes + 1])).Code);
        Assert.AreEqual(AiFailureCode.AttachmentLimitExceeded,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create("big.txt", Encoding.UTF8.GetBytes(new string('a', 12001)))).Code);
        Assert.AreEqual(AiFailureCode.AttachmentLimitExceeded,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachment.Create("big.txt", new byte[AiChatAttachmentPolicy.MaximumTextBytes + 1])).Code);
        var text = AiChatAttachment.Create("notes.txt", Encoding.UTF8.GetBytes(new string('a', 10000)));
        Assert.AreEqual(10000, text.Text!.Length);
        AiChatAttachmentPolicy.Validate([text, text]);
        Assert.AreEqual(AiFailureCode.AttachmentLimitExceeded,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachmentPolicy.Validate([text, text, text])).Code);
        Assert.AreEqual(AiFailureCode.AttachmentLimitExceeded,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachmentPolicy.Validate([text, text, text, text, text])).Code);
        var large = AiChatAttachment.Create("large.png", LargePng());
        AiChatAttachmentPolicy.Validate([large, large]);
        Assert.AreEqual(AiFailureCode.AttachmentLimitExceeded,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachmentPolicy.Validate([large, large, large])).Code);
        AiChatAttachmentPolicy.Validate(null);
        Assert.AreEqual(AiFailureCode.AttachmentInvalid,
            Assert.ThrowsExactly<ManagedAiException>(() => AiChatAttachmentPolicy.Validate(new AiChatAttachment[] { null! })).Code);
    }

    [TestMethod]
    public async Task AttachmentOnlyTurnSnapshotsDraftAndHistoryContainsOnlySanitizedNames()
    {
        var provider = new RecordingProvider();
        var session = new ManagedAiChatSession(provider, new Browser());
        var text = AiChatAttachment.Create("010-1234-5678.txt", Encoding.UTF8.GetBytes("unique attachment body"));
        var image = AiChatAttachment.Create("screen.png", Png());
        var draft = new List<AiChatAttachment> { text, image };
        await session.SendAsync("", null, CancellationToken.None, attachments: draft);
        draft.Clear();
        Assert.HasCount(2, provider.Requests[0].Attachments!);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<AiChatAttachment>)provider.Requests[0].Attachments!).Clear());
        Assert.Contains("[전화번호].txt", session.History[0].Text);
        Assert.Contains("screen.png", session.History[0].Text);
        Assert.DoesNotContain("unique attachment body", session.History[0].Text);
        Assert.DoesNotContain("010-1234", session.History[0].Text);
        await session.SendAsync("follow-up", null, CancellationToken.None);
        Assert.IsNull(provider.Requests[1].Attachments);
        Assert.DoesNotContain("unique attachment body", string.Join("", provider.Requests[1].History.Select(message => message.Text)));
        for (var i = 0; i < 10; i++)
            await session.SendAsync(new string('a', 3900), null, CancellationToken.None, attachments: [text]);
        Assert.IsLessThanOrEqualTo(12, session.History.Count);
        Assert.IsLessThanOrEqualTo(20000, session.History.Sum(message => message.Text.Length));
    }

    [TestMethod]
    public async Task FailedTurnPreservesImmutableDraftForRetryAndInvalidBatchNeverReachesProvider()
    {
        var provider = new RecordingProvider { Fail = true };
        var session = new ManagedAiChatSession(provider, new Browser());
        var raw = Png();
        var attachment = AiChatAttachment.Create("screen.png", raw);
        var expected = attachment.GetContent();
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => session.SendAsync("", null, CancellationToken.None, attachments: [attachment]));
        Assert.HasCount(0, session.History);
        raw[0] = 0;
        provider.Requests[0].Attachments![0].GetContent()[0] = 0;
        provider.Fail = false;
        await session.SendAsync("", null, CancellationToken.None, attachments: [attachment]);
        CollectionAssert.AreEqual(expected, provider.Requests[1].Attachments![0].GetContent());
        var count = provider.Requests.Count;
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => session.SendAsync("invalid", null, CancellationToken.None,
            attachments: Enumerable.Repeat(attachment, 5).ToArray()));
        Assert.HasCount(count, provider.Requests);
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => session.SendAsync(" ", null, CancellationToken.None));
    }

    private static byte[] LargePng()
    {
        var original = Png();
        var length = 6 * 1024 * 1024;
        var result = new byte[original.Length + length + 12];
        original.AsSpan(0, 33).CopyTo(result);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(33), (uint)length);
        Encoding.ASCII.GetBytes("raNd").CopyTo(result, 37);
        result.AsSpan(41, length).Fill((byte)'a');
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(41 + length), Crc(result.AsSpan(37, length + 4)));
        original.AsSpan(33).CopyTo(result.AsSpan(45 + length));
        return result;
    }

    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
        return ~crc;
    }

    private sealed class RecordingProvider : IManagedAiChatProvider
    {
        public bool Fail;
        public List<AiChatRequest> Requests { get; } = [];
        public Task<AiChatResponse> ChatAsync(AiChatRequest request, IProgress<AiProgress>? progress, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Fail) throw new ManagedAiException(AiFailureCode.ProviderNetworkUnavailable);
            return Task.FromResult(new AiChatResponse("ok", DateTimeOffset.UtcNow, 0));
        }
    }

    private sealed class Browser : ITableClothBrowser
    {
        public Task OpenAsync(Uri target, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

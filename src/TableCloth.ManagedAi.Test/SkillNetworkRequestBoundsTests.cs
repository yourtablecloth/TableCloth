using System.Text;
using System.Text.Json;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillNetworkRequestBoundsTests
{
    [TestMethod]
    public async Task GrowingWorkerFileCannotChooseTheHostAllocationOrReadBudget()
    {
        // No large backing data: the virtual file keeps growing and fails if Length is consulted.
        using var worker = new VirtualGrowingStream();
        var buffer = new byte[SkillNetworkBridge.MaximumRequestBytes + 1];

        var length = await SkillNetworkBridge.ReadBoundedRequestAsync(worker, buffer, CancellationToken.None);

        Assert.AreEqual(SkillNetworkBridge.MaximumRequestBytes + 1, length);
        Assert.AreEqual(length, worker.TotalBytesRead);
        Assert.AreEqual(0, worker.LengthReads);
        Assert.IsLessThanOrEqualTo(buffer.Length, worker.LargestRequestedRead);
        Assert.IsGreaterThan(SkillNetworkBridge.MaximumRequestBytes, length, "An oversized request must not be parsed as a valid truncated prefix.");
    }

    [TestMethod]
    public async Task PartialReadsAreCombinedBeforeParsingTheCompleteRequest()
    {
        var payload = Encoding.UTF8.GetBytes("{\"id\":7,\"method\":\"GET\",\"url\":\"https://ecos.bok.or.kr/api/KeyStatisticList/sample/json/kr/1/1/\"}");
        using var worker = new PartialReadStream(payload, maximumChunk: 3);
        var buffer = new byte[SkillNetworkBridge.MaximumRequestBytes + 1];

        var length = await SkillNetworkBridge.ReadBoundedRequestAsync(worker, buffer, CancellationToken.None);

        Assert.AreEqual(payload.Length, length);
        using var parsed = JsonDocument.Parse(buffer.AsMemory(0, length));
        Assert.AreEqual(7, parsed.RootElement.GetProperty("id").GetInt32());
    }

    [TestMethod]
    public async Task EqualLengthReplacementIsReadFromTheBeginningOnEveryPoll()
    {
        using var worker = new MemoryStream();
        var first = Encoding.UTF8.GetBytes("{\"id\":1}");
        var second = Encoding.UTF8.GetBytes("{\"id\":2}");
        worker.Write(first);
        var buffer = new byte[SkillNetworkBridge.MaximumRequestBytes + 1];
        var firstLength = await SkillNetworkBridge.ReadBoundedRequestAsync(worker, buffer, CancellationToken.None);
        using (var parsed = JsonDocument.Parse(buffer.AsMemory(0, firstLength)))
            Assert.AreEqual(1, parsed.RootElement.GetProperty("id").GetInt32());
        worker.Position = 0;
        worker.Write(second);

        var secondLength = await SkillNetworkBridge.ReadBoundedRequestAsync(worker, buffer, CancellationToken.None);

        Assert.AreEqual(firstLength, secondLength);
        using var replacement = JsonDocument.Parse(buffer.AsMemory(0, secondLength));
        Assert.AreEqual(2, replacement.RootElement.GetProperty("id").GetInt32());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public async Task SentinelDistinguishesExactLimitFromOversizedValidJsonPrefix(int extraBytes)
    {
        var prefix = Encoding.UTF8.GetBytes("{\"id\":1}");
        var payload = new byte[SkillNetworkBridge.MaximumRequestBytes + extraBytes];
        payload.AsSpan().Fill((byte)' ');
        prefix.CopyTo(payload, 0);
        using var worker = new PartialReadStream(payload, maximumChunk: 257);
        var buffer = new byte[SkillNetworkBridge.MaximumRequestBytes + 1];

        var length = await SkillNetworkBridge.ReadBoundedRequestAsync(worker, buffer, CancellationToken.None);

        Assert.AreEqual(payload.Length, length);
        Assert.AreEqual(extraBytes == 0, length <= SkillNetworkBridge.MaximumRequestBytes);
        if (extraBytes == 0)
        {
            using var parsed = JsonDocument.Parse(buffer.AsMemory(0, length));
            Assert.AreEqual(1, parsed.RootElement.GetProperty("id").GetInt32());
        }
    }

    [TestMethod]
    public async Task TruncatedWriterSnapshotDoesNotBecomeAValidRequest()
    {
        using var worker = new PartialReadStream(Encoding.UTF8.GetBytes("{\"id\":1,\"url\":"), maximumChunk: 2);
        var buffer = new byte[SkillNetworkBridge.MaximumRequestBytes + 1];

        var length = await SkillNetworkBridge.ReadBoundedRequestAsync(worker, buffer, CancellationToken.None);

        Assert.Throws<JsonException>(() => JsonDocument.Parse(buffer.AsMemory(0, length)));
    }

    private sealed class VirtualGrowingStream : Stream
    {
        private long _position;
        internal int LengthReads { get; private set; }
        internal int TotalBytesRead { get; private set; }
        internal int LargestRequestedRead { get; private set; }
        public override long Length { get { LengthReads++; throw new InvalidOperationException("Worker-controlled Length must not be consulted."); } }
        public override long Position { get => _position; set => _position = value; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            LargestRequestedRead = Math.Max(LargestRequestedRead, buffer.Length);
            var count = Math.Min(buffer.Length, 97);
            buffer.Span[..count].Fill((byte)' ');
            _position += count;
            TotalBytesRead += count;
            return ValueTask.FromResult(count);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class PartialReadStream(byte[] bytes, int maximumChunk) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, maximumChunk)], token);
    }
}

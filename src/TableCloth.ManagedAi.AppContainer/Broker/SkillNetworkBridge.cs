using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

// The worker retains NO network capability. Only these public read-only API requests can be
// performed by the host. This is not a general HTTP proxy or permission to access other hosts.
internal sealed class SkillNetworkBridge : IAsyncDisposable
{
    internal static readonly string[] KnownHosts = ["ecos.bok.or.kr", "k-skill-proxy.nomadamas.org"];
    private const int MaximumResponseBytes = 1048576;
    private const int MaximumRequestBytes = 16384;
    private readonly IReadOnlyList<string> _hosts;
    private readonly FileStream _request;
    private readonly FileStream _response;
    private readonly HttpClient _client;
    private readonly Func<Uri, CancellationToken, Task<Reply>>? _fetch;
    internal string DirectoryPath { get; }

    internal sealed record Reply(int Status, IReadOnlyDictionary<string, string> Headers, byte[] Body);
    private sealed record RpcResponse(int Id, Reply? Reply, string? Error);

    internal SkillNetworkBridge(string workDirectory, IReadOnlyList<string> hosts,
        Func<Uri, CancellationToken, Task<Reply>>? fetch = null)
    {
        if (hosts.Count is < 1 or > 2 || hosts.Any(x => !KnownHosts.Contains(x, StringComparer.Ordinal)))
            throw BrokerFiles.Invalid();
        _hosts = hosts;
        _fetch = fetch; // Offline test seam, never a manifest or MCP property.
        DirectoryPath = Path.Combine(workDirectory, "network");
        BrokerFiles.RequireUnder(workDirectory, DirectoryPath);
        BrokerFiles.RejectReparsePoints(DirectoryPath);
        Directory.CreateDirectory(DirectoryPath);
        // Keep the files open without FileShare.Delete so a worker cannot replace their paths
        // with a reparse point while the host reads and writes through these stable handles.
        // Disable FileStream buffering: the other process overwrites these handles between calls,
        // including requests of equal length, which must never reuse cached request bytes.
        _request = new(Path.Combine(DirectoryPath, "request.json"), FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.ReadWrite, bufferSize: 1, FileOptions.Asynchronous);
        try { _response = new(Path.Combine(DirectoryPath, "response.json"), FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.ReadWrite, bufferSize: 1, FileOptions.Asynchronous); }
        catch { _request.Dispose(); throw; }
        _client = new(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            MaxResponseHeadersLength = 16,
            ConnectCallback = ConnectPublicAsync
        }) { Timeout = Timeout.InfiniteTimeSpan };
    }

    internal async Task RunAsync(CancellationToken token)
    {
        var previousId = 0;
        var requestCount = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            if (_request.Length is > 0 and <= MaximumRequestBytes)
            {
                var bytes = new byte[(int)_request.Length];
                _request.Position = 0;
                var length = await _request.ReadAsync(bytes, token);
                JsonDocument? document = null;
                try { document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 8 }); }
                catch (JsonException) { /* The worker may still be writing its request. */ }
                using (document)
                {
                    if (document is not null && document.RootElement.ValueKind == JsonValueKind.Object &&
                        document.RootElement.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.Number &&
                        idValue.TryGetInt32(out var id) && id > previousId)
                    {
                        previousId = id;
                        var response = await HandleAsync(document.RootElement, ++requestCount, token);
                        var encoded = SerializeResponse(response);
                        _response.Position = 0;
                        _response.SetLength(0);
                        await _response.WriteAsync(encoded, token);
                        await _response.FlushAsync(token);
                    }
                }
            }
            await Task.Delay(10, token);
        }
    }

    private async Task<RpcResponse> HandleAsync(JsonElement request, int requestCount, CancellationToken token)
    {
        var id = request.GetProperty("id").GetInt32();
        try
        {
            BrokerFiles.ValidateJson(request);
            BrokerFiles.ObjectFields(request, ["id", "method", "url", "headers"]);
            if (requestCount > 20 || BrokerFiles.Text(request, "method", 8) != "GET" ||
                !TryValidateUrl(BrokerFiles.Text(request, "url", 2048), _hosts, out var uri)) throw BrokerFiles.Invalid();
            if (request.TryGetProperty("headers", out var headers))
            {
                if (headers.ValueKind != JsonValueKind.Object || headers.EnumerateObject().Count() > 16) throw BrokerFiles.Invalid();
                foreach (var header in headers.EnumerateObject())
                    if (!new[] { "accept", "user-agent", "accept-encoding" }.Contains(header.Name.ToLowerInvariant(), StringComparer.Ordinal) ||
                        header.Value.ValueKind != JsonValueKind.String || header.Value.GetString()!.Length > 512 ||
                        header.Value.GetString()!.Any(char.IsControl)) throw BrokerFiles.Invalid();
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var reply = await (_fetch is null ? FetchAsync(uri, deadline.Token) : _fetch(uri, deadline.Token));
            if (reply.Body.Length > MaximumResponseBytes) throw BrokerFiles.Invalid();
            return new(id, reply, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { return new(id, null, "network-request-blocked-or-unavailable"); }
    }

    private static byte[] SerializeResponse(RpcResponse response)
    {
        // The release broker is NativeAOT; never reflect over anonymous/runtime JSON types.
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", response.Id);
            if (response.Reply is { } reply)
            {
                writer.WriteNumber("status", reply.Status);
                writer.WriteStartObject("headers");
                foreach (var header in reply.Headers) writer.WriteString(header.Key, header.Value);
                writer.WriteEndObject();
                writer.WriteBase64String("body", reply.Body);
            }
            else writer.WriteString("error", response.Error);
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private async Task<Reply> FetchAsync(Uri uri, CancellationToken token)
    {
        for (var redirect = 0; redirect < 4; redirect++)
        {
            if (!TryValidateUrl(uri.AbsoluteUri, _hosts, out uri)) throw BrokerFiles.Invalid();
            using var message = new HttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.UserAgent.ParseAdd("TableCloth-Skill-Bridge/1.0");
            using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location ?? throw BrokerFiles.Invalid();
                uri = location.IsAbsoluteUri ? location : new(uri, location);
                continue;
            }
            if (response.Content.Headers.ContentLength is > MaximumResponseBytes) throw BrokerFiles.Invalid();
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, token)) != 0)
            {
                if (body.Length + read > MaximumResponseBytes) throw BrokerFiles.Invalid();
                await body.WriteAsync(buffer.AsMemory(0, read), token);
            }
            var headers = new Dictionary<string, string>();
            if (response.Content.Headers.ContentType is { } contentType) headers["Content-Type"] = contentType.ToString();
            return new((int)response.StatusCode, headers, body.ToArray());
        }
        throw BrokerFiles.Invalid();
    }

    internal static bool TryValidateUrl(string text, IReadOnlyList<string> hosts, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2048 || text.Any(x => char.IsControl(x) || char.IsWhiteSpace(x) || x == '\\') ||
            !Uri.TryCreate(text, UriKind.Absolute, out var parsed) || parsed.Scheme != "https" || parsed.Port != 443 ||
            parsed.UserInfo.Length != 0 || parsed.Fragment.Length != 0 || parsed.HostNameType != UriHostNameType.Dns ||
            !hosts.Contains(parsed.IdnHost, StringComparer.Ordinal) || !KnownHosts.Contains(parsed.IdnHost, StringComparer.Ordinal)) return false;
        var path = parsed.AbsolutePath;
        if (path.Contains("%2f", StringComparison.OrdinalIgnoreCase) || path.Contains("%5c", StringComparison.OrdinalIgnoreCase)) return false;
        if (parsed.IdnHost == "ecos.bok.or.kr")
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 7 || parts[0] != "api" || parts[2] != "sample" || parts[3] != "json" || parts[4] != "kr" ||
                !new[] { "StatisticSearch", "StatisticTableList", "StatisticItemList", "KeyStatisticList", "StatisticWord" }.Contains(parts[1], StringComparer.Ordinal)) return false;
        }
        else if (!new[] { "/v1/kosis/search", "/v1/kosis/meta", "/v1/kosis/data", "/v1/kosis/list", "/v1/kosis/explain", "/v1/kosis/indicator" }
            .Contains(path, StringComparer.Ordinal)) return false;
        uri = parsed;
        return true;
    }

    private async ValueTask<Stream> ConnectPublicAsync(SocketsHttpConnectionContext context, CancellationToken token)
    {
        if (context.DnsEndPoint.Port != 443 || !_hosts.Contains(context.DnsEndPoint.Host, StringComparer.Ordinal)) throw BrokerFiles.Invalid();
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
        if (addresses.Length == 0 || addresses.Any(x => !IsPublicAddress(x))) throw BrokerFiles.Invalid();
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, 443), token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch { socket.Dispose(); if (token.IsCancellationRequested) throw; }
        }
        throw new IOException("The reviewed public API is unavailable.");
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
            return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 &&
                !(bytes[0] == 169 && bytes[1] == 254) && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
                !(bytes[0] == 192 && bytes[1] == 168) && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
                !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2) &&
                !(bytes[0] == 198 && (bytes[1] is 18 or 19 || bytes[1] == 51 && bytes[2] == 100)) &&
                !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        return address.AddressFamily == AddressFamily.InterNetworkV6 && (bytes[0] & 0xe0) == 0x20 && address.ScopeId == 0 &&
            !(bytes[0] == 0x20 && bytes[1] == 0x02) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _request.DisposeAsync();
        await _response.DisposeAsync();
    }
}

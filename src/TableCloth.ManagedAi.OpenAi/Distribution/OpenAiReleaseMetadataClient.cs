using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TableCloth.ManagedAi.OpenAi;

public sealed record ReleaseAsset(string Name, string Sha256, Uri Url);
public sealed record CodexRelease(string Version, string Target, ReleaseAsset Package, ReleaseAsset Checksums);

public sealed partial class OpenAiReleaseMetadataClient(IHttpClientFactory clients, ManagedAiOptions options)
{
    public const string HttpClientName = "TableCloth.ManagedAi.OpenAi";
    public const string Latest = "https://releases.openai.com/codex/channels/latest";

    public async Task<CodexRelease> GetAsync(string? version, CancellationToken cancellationToken)
    {
        if (version is not null) ValidateVersion(version);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var memory = new MemoryStream();
            await DownloadAsync(new Uri(version is null ? Latest :
                $"https://releases.openai.com/codex/releases/{version}/release.json"), memory, 2 * 1024 * 1024, timeout.Token);
            var release = Parse(memory.ToArray(), RuntimeInformation.OSArchitecture);
            if (version is not null && version != release.Version) throw Unknown();
            return release;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new ManagedAiException(AiFailureCode.OfficialMetadataUnavailable);
        }
    }

    public static CodexRelease Parse(ReadOnlyMemory<byte> json, Architecture architecture)
    {
        var target = architecture switch
        {
            Architecture.X64 => "x86_64-pc-windows-msvc",
            Architecture.Arm64 => "aarch64-pc-windows-msvc",
            _ => throw new ManagedAiException(AiFailureCode.UnsupportedArchitecture)
        };
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var tag = document.RootElement.GetProperty("tag_name").GetString()!;
            if (!tag.StartsWith("rust-v", StringComparison.Ordinal)) throw Unknown();
            var version = tag[6..];
            ValidateVersion(version);
            var assets = document.RootElement.GetProperty("assets").EnumerateArray().ToArray();
            if (assets.GroupBy(x => x.GetProperty("name").GetString(), StringComparer.Ordinal).Any(x => x.Count() != 1))
                throw Unknown();
            ReleaseAsset Select(string name)
            {
                var asset = assets.Single(x => x.GetProperty("name").GetString() == name);
                var digest = asset.GetProperty("digest").GetString()!;
                if (!DigestPattern().IsMatch(digest)) throw Unknown();
                var url = new Uri(asset.GetProperty("browser_download_url").GetString()!, UriKind.Absolute);
                ValidateUrl(url);
                if (url.AbsolutePath != $"/codex/releases/{version}/{name}" || url.Query.Length != 0) throw Unknown();
                return new(name, digest[7..].ToLowerInvariant(), url);
            }
            return new(version, target, Select($"codex-package-{target}.tar.gz"), Select("codex-package_SHA256SUMS"));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or
            ArgumentException or NullReferenceException or UriFormatException) { throw Unknown(); }
    }

    public async Task<string> DownloadAsync(Uri url, Stream destination, long maximum, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.DownloadTimeout);
        using var client = clients.CreateClient(HttpClientName);
        for (var redirects = 0; ; redirects++)
        {
            ValidateUrl(url);
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            // The named client MUST have AllowAutoRedirect=false, otherwise a cross-host request could already have happened.
            if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or
                HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                if (redirects >= 3 || response.Headers.Location is null) throw Unknown();
                url = new Uri(url, response.Headers.Location);
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > maximum) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long total = 0;
            int count;
            while ((count = await source.ReadAsync(buffer, timeout.Token)) != 0)
            {
                total += count;
                if (total > maximum) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
                hash.AppendData(buffer, 0, count);
                await destination.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
            }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
    }

    public static void ValidateUrl(Uri url)
    {
        if (!url.IsAbsoluteUri || url.Scheme != "https" || url.IdnHost != "releases.openai.com" ||
            !url.IsDefaultPort || url.UserInfo.Length != 0 || url.Fragment.Length != 0 ||
            !url.AbsolutePath.StartsWith("/codex/", StringComparison.Ordinal)) throw Unknown();
    }
    public static void ValidateVersion(string version)
    {
        if (version.Length > 50 || !VersionPattern().IsMatch(version)) throw Unknown();
    }
    [GeneratedRegex(@"\A[0-9]+\.[0-9]+\.[0-9]+(?:-alpha(?:\.[0-9]+){0,2}|-beta(?:\.[0-9]+)?)?\z")]
    private static partial Regex VersionPattern();
    [GeneratedRegex(@"\Asha256:[0-9a-fA-F]{64}\z")]
    private static partial Regex DigestPattern();
    private static ManagedAiException Unknown() => new(AiFailureCode.ReleaseFormatUnknown);
}

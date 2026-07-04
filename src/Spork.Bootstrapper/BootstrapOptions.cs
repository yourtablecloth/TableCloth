using System.Text.Json.Serialization;

namespace Spork.Bootstrapper;

/// <summary>
/// 부트스트래퍼 인자 계약(docs/EXPRESS_BOOTSTRAPPER_DESIGN.md §5). `.ps1` shim 이 포워딩하는
/// 값을 받는다. 인자가 전혀 없어도(사용자가 exe 만 단독 실행) GitHub 폴백으로 동작한다.
/// </summary>
internal sealed class BootstrapOptions
{
    /// <summary>`{arch}` 토큰 포함 zip URL 템플릿. 없거나 미치환 플레이스홀더면 GitHub 폴백.</summary>
    public string? ZipUrlTemplate { get; private init; }

    /// <summary>공백 구분 사이트 Id. Spork.exe 위치 인자로 그대로 전달. 비면 일반 런처.</summary>
    public string? SiteIds { get; private init; }

    /// <summary>`x64=hash;arm64=hash` 체크섬 맵. 해당 arch 항목이 있으면 검증.</summary>
    private Dictionary<string, string> Sha256Map { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>GitHub 폴백 대상 owner/repo.</summary>
    public string GithubRepo { get; private init; } = "yourtablecloth/TableCloth";

    /// <summary>해제 대상. 기본 %USERPROFILE%\Desktop\Spork.</summary>
    public string? Dest { get; private init; }

    /// <summary>UI 언어 강제(`ko`, `en` 등). 없으면 OS UI 언어 자동 감지. 호스트 언어를 맞추고 싶을 때 사용.</summary>
    public string? Lang { get; private init; }

    public string? Sha256For(string arch) =>
        Sha256Map.TryGetValue(arch, out var hash) ? hash : null;

    public static BootstrapOptions Parse(string[] args)
    {
        string? template = null, siteIds = null, repo = null, dest = null, sha256Map = null, lang = null;

        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            string? value = i + 1 < args.Length ? args[i + 1] : null;
            switch (key)
            {
                case "--zip-url-template": template = value; i++; break;
                case "--site-ids": siteIds = value; i++; break;
                case "--sha256-map": sha256Map = value; i++; break;
                case "--github-repo": repo = value; i++; break;
                case "--dest": dest = value; i++; break;
                case "--lang": lang = value; i++; break;
            }
        }

        return new BootstrapOptions
        {
            ZipUrlTemplate = Meaningful(template) ? template : null,
            SiteIds = Meaningful(siteIds) ? siteIds : null,
            Dest = Meaningful(dest) ? dest : null,
            Lang = Meaningful(lang) ? lang : null,
            GithubRepo = Meaningful(repo) ? repo! : "yourtablecloth/TableCloth",
            Sha256Map = ParseSha256Map(sha256Map),
        };
    }

    // 값이 비었거나, 치환되지 않은 플레이스홀더(__SPORK_..__)면 "없음"으로 취급.
    private static bool Meaningful(string? v) =>
        !string.IsNullOrWhiteSpace(v) && !v.Contains("__SPORK", StringComparison.Ordinal);

    private static Dictionary<string, string> ParseSha256Map(string? raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!Meaningful(raw))
            return map;

        foreach (var pair in raw!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0 || eq >= pair.Length - 1)
                continue;
            string arch = pair[..eq].Trim();
            string hash = pair[(eq + 1)..].Trim();
            if (arch.Length > 0 && hash.Length > 0)
                map[arch] = hash;
        }
        return map;
    }
}

/// <summary>GitHub Releases API 응답의 최소 모델(폴백 전용).</summary>
internal sealed class GitHubRelease
{
    [JsonPropertyName("assets")]
    public GitHubAsset[]? Assets { get; set; }
}

internal sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
}

/// <summary>
/// System.Text.Json 소스 생성 컨텍스트. 리플렉션 기반 직렬화를 피해 NativeAOT/트리밍에서
/// 경고 없이 GitHub 릴리스 JSON 을 파싱한다.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(GitHubRelease))]
internal partial class BootstrapJsonContext : JsonSerializerContext
{
}

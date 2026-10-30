#!/usr/bin/env dotnet
#:sdk Cadenza@1.0.15
#:property PublishAot=false

// GITHUB_TOKEN reads this repository's release. GITHUB_PAT authenticates the
// external winget submission, via an environment variable, never an argument.
// --self-test exercises the same read-only checks with an in-memory HTTP handler.
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

const string PackageId = "TableClothProject.TableCloth";
const string Repo = "yourtablecloth/TableCloth";
const string WingetRepo = "microsoft/winget-pkgs";
const string ManifestPath = "manifests/t/TableClothProject/TableCloth";
const string WingetCreateUrl = "https://aka.ms/wingetcreate/latest";

if (args is ["--self-test"])
    return await RunSelfTests();

void Summary(string markdown)
{
    var path = Env.Get("GITHUB_STEP_SUMMARY");
    if (!string.IsNullOrEmpty(path))
        File.AppendAllText(path, markdown + Environment.NewLine);
}

try
{
    var tag = (Env.Get("RELEASE_TAG") ?? string.Empty).Trim();
    var version = ValidateTag(tag);
    var verifyOnly = ParseVerifyOnly(Env.Get("VERIFY_ONLY"));
    var token = Env.Get("GITHUB_PAT") ?? string.Empty;
    if (token.Length == 0)
        throw new InvalidOperationException("TABLECLOTH_GITHUB_PAT 시크릿이 비어 있습니다. public_repo 권한을 가진 지원 자격 증명을 등록하면 제출할 수 있습니다.");
    if (token.StartsWith("github_pat_", StringComparison.Ordinal))
        throw new InvalidOperationException("wingetcreate는 fine-grained PAT을 지원하지 않습니다. TABLECLOTH_GITHUB_PAT에 public_repo 권한을 가진 classic PAT을 등록하면 제출할 수 있습니다.");

    // Validate the submission credential before every idempotent skip. Successful
    // repository-token reads must never hide a broken external credential.
    using var submissionClient = CreateGitHubClient(token);
    var login = await ValidateSubmissionCredential(submissionClient);
    WriteLine($"winget 제출 인증 확인: {login}");
    var readToken = Env.Get("GITHUB_TOKEN") ?? string.Empty;
    using var releaseClient = CreateGitHubClient(readToken.Length == 0 ? token : readToken,
        credential: readToken.Length == 0 ? GitHubCredential.Submission : GitHubCredential.Repository);
    var release = await GetJson(releaseClient, $"repos/{Repo}/releases/tags/{tag}",
        GhJson.Default.GhRelease, $"릴리스 '{tag}' 조회", readToken.Length == 0 ? "TABLECLOTH_GITHUB_PAT" : "GITHUB_TOKEN");
    if (release.tag_name != tag || release.draft)
        throw new InvalidOperationException($"릴리스 '{tag}'의 태그 또는 게시 상태가 올바르지 않습니다. draft 릴리스는 제출하지 않습니다.");
    if (release.prerelease)
    {
        WriteLine($"::notice::릴리스 '{tag}'는 prerelease이므로 winget 제출을 건너뜁니다.");
        return 0;
    }

    var installers = SelectInstallers(release, version);
    WriteLine($"x64 설치 관리자: {installers[0].browser_download_url}");
    WriteLine($"arm64 설치 관리자: {installers[1].browser_download_url}");
    var existing = await FindExistingSubmission(submissionClient, login, version, installers);
    if (existing is not null)
    {
        WriteLine($"::notice::{PackageId} {version}의 기존 매니페스트와 설치 관리자 정보를 확인했습니다. 제출을 건너뜁니다: {existing}");
        Summary($"### winget 기존 제출 확인\n\n`{PackageId}` **{version}**의 매니페스트와 설치 관리자 URL을 검증했습니다.\n\n{existing}\n\n제출 인증도 확인했으며 포크 동기화와 PR 제출을 수행하지 않았습니다.");
        return 0;
    }
    if (verifyOnly)
    {
        WriteLine($"::notice::{PackageId} {version}의 인증, 릴리스, 두 설치 관리자 및 기존 제출 여부를 확인했습니다. verify_only이므로 외부 변경을 수행하지 않습니다.");
        Summary($"### winget 읽기 검증 완료\n\n`{PackageId}` **{version}**의 제출 인증, 릴리스, 두 설치 관리자 및 중복 제출 검사를 통과했습니다. 포크 동기화와 PR 제출은 수행하지 않았습니다.");
        return 0;
    }

    await SyncWingetFork(submissionClient, login);
    using var temporary = Fs.TempDir();
    var executable = Path.Combine(temporary.Path, "wingetcreate.exe");
    // The Cadenza download client never receives either authorization header.
    await Http.Download(WingetCreateUrl, executable);
    var previousToken = Environment.GetEnvironmentVariable("WINGET_CREATE_GITHUB_TOKEN");
    int exit;
    try
    {
        Environment.SetEnvironmentVariable("WINGET_CREATE_GITHUB_TOKEN", token);
        WriteLine($"wingetcreate update {PackageId} --version {version} --urls (x64, arm64) --submit --no-open");
        exit = Sh.Run($"\"{executable}\" update {PackageId} --version {version} " +
            $"--urls \"{installers[0].browser_download_url}|x64\" \"{installers[1].browser_download_url}|arm64\" --submit --no-open");
    }
    finally
    {
        Environment.SetEnvironmentVariable("WINGET_CREATE_GITHUB_TOKEN", previousToken);
    }
    if (exit != 0)
        throw new InvalidOperationException($"wingetcreate가 종료 코드 {exit}로 실패했습니다.");
    WriteLine($"::notice::{PackageId} {version}의 winget 매니페스트 PR을 제출했습니다.");
    Summary($"### winget 제출 완료\n\n`{PackageId}` **{version}** (태그 `{tag}`)\n\n[제출 PR 확인](https://github.com/{WingetRepo}/pulls?q=is%3Apr+{Uri.EscapeDataString(PackageId)})");
    return 0;
}
catch (Exception exception)
{
    WriteLine($"::error::{exception.Message}");
    return 1;
}

string ValidateTag(string tag)
{
    if (!Regex.IsMatch(tag, @"\Av\d+\.\d+\.\d+\z", RegexOptions.CultureInvariant))
        throw new InvalidOperationException("RELEASE_TAG는 vX.Y.Z 형식의 정식 릴리스 태그만 허용합니다. Preview와 기타 접미사는 제출하지 않습니다.");
    return tag[1..];
}

bool ParseVerifyOnly(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return false;
    if (bool.TryParse(value, out var result)) return result;
    throw new InvalidOperationException("VERIFY_ONLY는 true 또는 false만 허용합니다.");
}

HttpClient CreateGitHubClient(string token, HttpMessageHandler? handler = null,
    GitHubCredential credential = GitHubCredential.Submission)
{
    // Header parser exceptions can echo their input. Reject malformed values
    // without quoting a credential before constructing an authorization header.
    // RFC 6750 section 2.1 b64token includes JWT dots and trailing '=' padding.
    if (!Regex.IsMatch(token, @"\A[A-Za-z0-9\-._~+/]+=*\z", RegexOptions.CultureInvariant))
    {
        var name = credential == GitHubCredential.Repository ? "GITHUB_TOKEN" : "TABLECLOTH_GITHUB_PAT";
        throw new InvalidOperationException($"{name} 자격 증명 형식이 올바르지 않습니다. 등록값의 공백 또는 잘못된 문자를 확인할 수 있습니다.");
    }
    var client = handler is null ? new HttpClient() : new HttpClient(handler);
    client.BaseAddress = new Uri("https://api.github.com/");
    client.Timeout = TimeSpan.FromSeconds(100);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("TableCloth-winget-submitter/1.1");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return client;
}

void CheckResponse(HttpResponseMessage response, string operation, string credential = "TABLECLOTH_GITHUB_PAT")
{
    if (response.IsSuccessStatusCode) return;
    var detail = response.StatusCode switch
    {
        HttpStatusCode.Unauthorized => $"{credential} 인증을 거부했습니다. 자격 증명의 만료, 폐기 또는 등록값을 확인하면 원인을 구분할 수 있습니다.",
        HttpStatusCode.Forbidden => $"{credential}의 권한, 조직 접근 정책 또는 API 사용 한도로 요청을 거부했습니다.",
        _ => "API 요청을 완료하지 못했습니다. 중복 제출을 방지하기 위해 중단합니다."
    };
    throw new InvalidOperationException($"{operation} 실패 (HTTP {(int)response.StatusCode}): {detail}");
}

async Task<T> GetJson<T>(HttpClient client, string path, JsonTypeInfo<T> type, string operation,
    string credential = "TABLECLOTH_GITHUB_PAT")
{
    using var response = await client.GetAsync(path);
    CheckResponse(response, operation, credential);
    return JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), type)
        ?? throw new InvalidOperationException($"{operation} 응답이 비어 있습니다.");
}

async Task<string> ValidateSubmissionCredential(HttpClient client)
{
    using var response = await client.GetAsync("user");
    CheckResponse(response, "winget 제출 자격 증명 검증");
    if (response.Headers.TryGetValues("X-OAuth-Scopes", out var header))
    {
        var scopes = string.Join(',', header).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (!scopes.Contains("public_repo", StringComparer.Ordinal) && !scopes.Contains("repo", StringComparer.Ordinal))
            throw new InvalidOperationException("TABLECLOTH_GITHUB_PAT에 public_repo 또는 repo 권한이 없습니다. public_repo 권한을 가진 classic PAT으로 제출할 수 있습니다.");
    }
    else
    {
        WriteLine("X-OAuth-Scopes 헤더가 없어 scope를 독립적으로 확인하지 못했습니다. GitHub 사용자 인증은 별도로 검증합니다.");
    }
    var user = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GhJson.Default.GhUser);
    if (string.IsNullOrWhiteSpace(user?.login))
        throw new InvalidOperationException("제출 자격 증명의 GitHub 사용자 정보를 확인하지 못했습니다.");
    return user.login;
}

GhAsset[] SelectInstallers(GhRelease release, string version)
{
    var installers = new List<GhAsset>();
    foreach (var architecture in new[] { "x64", "arm64" })
    {
        var name = $"TableCloth_{version}.0_Release_{architecture}.exe";
        var matches = (release.assets ?? []).Where(asset => asset.name == name).ToArray();
        if (matches.Length != 1 || matches[0].size <= 0 || matches[0].browser_download_url !=
            $"https://github.com/{Repo}/releases/download/{release.tag_name}/{name}")
            throw new InvalidOperationException($"릴리스에서 {architecture} 설치 관리자 '{name}'의 고유한 공식 URL과 파일 크기를 확인하지 못했습니다.");
        installers.Add(matches[0]);
    }
    return installers.ToArray();
}

async Task<string?> FindExistingSubmission(HttpClient client, string login, string version, GhAsset[] installers)
{
    var directory = $"{ManifestPath}/{version}/";
    using (var response = await client.GetAsync($"repos/{WingetRepo}/contents/{directory}"))
    {
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            CheckResponse(response, "winget 등록 버전 조회");
            var files = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), GhJson.Default.GhFileArray) ?? [];
            await VerifyManifestFiles(client, WingetRepo, files, directory, version, installers);
            return $"https://github.com/{WingetRepo}/tree/master/{directory.TrimEnd('/')}";
        }
    }

    // Partial results or a failed request never count as evidence of no PR.
    // Exact title boundaries avoid false matches such as version 1.22.0.1.
    var query = $"repo:{WingetRepo} is:pr is:open in:title {PackageId} {version}";
    var search = await GetJson(client, $"search/issues?q={Uri.EscapeDataString(query)}&per_page=100",
        GhJson.Default.GhSearch, "기존 winget PR 검색");
    if (search.incomplete_results != false || search.total_count is null or < 0 ||
        search.items is null || search.total_count != search.items.Length)
        throw new InvalidOperationException("기존 winget PR 검색 결과가 불완전하므로 중복 제출을 방지하기 위해 중단합니다.");
    var candidates = new List<GhIssue>(search.items);
    // Search indexing can lag immediately after a successful submission. Read
    // the submitting user's open PRs independently before concluding absence.
    for (var page = 1; page <= 10; page++)
    {
        var ownIssues = await GetJson(client,
            $"repos/{WingetRepo}/issues?creator={Uri.EscapeDataString(login)}&state=open&per_page=100&page={page}",
            GhJson.Default.GhIssueArray, "제출 사용자의 기존 winget PR 목록 조회");
        if (ownIssues.Length > 100)
            throw new InvalidOperationException("제출 사용자의 기존 PR 목록 응답이 올바르지 않아 중단합니다.");
        candidates.AddRange(ownIssues.Where(issue => issue.pull_request.HasValue));
        if (ownIssues.Length < 100) break;
        if (page == 10)
            throw new InvalidOperationException("제출 사용자의 기존 PR 목록이 검증 한도를 넘어 중복 제출을 방지하기 위해 중단합니다.");
    }
    foreach (var issue in candidates.DistinctBy(candidate => candidate.number))
    {
        if (!MatchesPackageVersion(issue.title, version)) continue;
        var pull = await GetJson(client, $"repos/{WingetRepo}/pulls/{issue.number}", GhJson.Default.GhPull, "기존 winget PR 조회");
        if (pull.state != "open" || pull.merged || pull.@base?.repo?.full_name != WingetRepo || pull.@base.@ref != "master" ||
            !Regex.IsMatch(pull.head?.sha ?? "", "\\A[0-9a-f]{40}\\z") ||
            !Regex.IsMatch(pull.head?.repo?.full_name ?? "", @"\A[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z") ||
            pull.changed_files < 2 || pull.changed_files > 100)
            throw new InvalidOperationException($"기존 PR #{issue.number}의 대상 저장소, 상태 또는 파일 목록을 확인하지 못했습니다.");
        var files = await GetJson(client, $"repos/{WingetRepo}/pulls/{issue.number}/files?per_page=100",
            GhJson.Default.GhFileArray, "기존 winget PR 파일 조회");
        if (files.Length != pull.changed_files)
            throw new InvalidOperationException($"기존 PR #{issue.number}의 파일 목록이 불완전합니다.");
        await VerifyManifestFiles(client, pull.head!.repo!.full_name, files, directory, version, installers);
        var after = await GetJson(client, $"repos/{WingetRepo}/pulls/{issue.number}", GhJson.Default.GhPull, "기존 winget PR 재확인");
        if (after.state != "open" || after.head?.sha != pull.head.sha)
            throw new InvalidOperationException($"검증 중 기존 PR #{issue.number}의 상태 또는 head SHA가 바뀌어 중단합니다.");
        return $"https://github.com/{WingetRepo}/pull/{issue.number}";
    }
    return null;
}

bool MatchesPackageVersion(string title, string version) =>
    Regex.IsMatch(title, $@"(?<![\w.]){Regex.Escape(PackageId)}(?![\w.])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase) &&
    Regex.IsMatch(title, $@"(?<![\w.+-])v?{Regex.Escape(version)}(?![\w.+-])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

async Task VerifyManifestFiles(HttpClient client, string repository, GhFile[] files, string directory,
    string version, GhAsset[] installers)
{
    var versionName = directory + PackageId + ".yaml";
    var installerName = directory + PackageId + ".installer.yaml";
    var paths = files.Select(file => file.filename ?? file.path ?? "").ToArray();
    if (!paths.Contains(versionName) || !paths.Contains(installerName) || paths.Distinct().Count() != paths.Length)
        throw new InvalidOperationException("기존 제출에서 버전 및 설치 관리자 매니페스트를 확인하지 못했습니다.");
    for (var index = 0; index < files.Length; index++)
    {
        var file = files[index];
        var path = paths[index];
        if (!path.StartsWith(directory, StringComparison.Ordinal) || !path.EndsWith(".yaml", StringComparison.Ordinal) ||
            path[directory.Length..].Contains('/') || file.status is "removed" or "renamed" ||
            !Regex.IsMatch(file.sha ?? "", "\\A[0-9a-f]{40}\\z"))
            throw new InvalidOperationException("기존 제출에 다른 버전의 파일 또는 검증할 수 없는 변경이 포함되어 있습니다.");
        // Immutable blobs avoid mutable/raw download URLs and token forwarding.
        var blob = await GetJson(client, $"repos/{repository}/git/blobs/{file.sha}", GhJson.Default.GhBlob, "기존 매니페스트 blob 조회");
        if (blob.encoding != "base64") throw new InvalidOperationException("기존 매니페스트 blob 인코딩을 확인하지 못했습니다.");
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(blob.content));
        if (YamlScalar(yaml, "PackageIdentifier") != PackageId || YamlScalar(yaml, "PackageVersion") != version)
            throw new InvalidOperationException("기존 매니페스트의 패키지 ID 또는 버전이 일치하지 않습니다.");
        if (path == versionName && YamlScalar(yaml, "ManifestType") != "version")
            throw new InvalidOperationException("기존 버전 매니페스트의 형식이 일치하지 않습니다.");
        if (path == installerName) VerifyInstallerManifest(yaml, installers);
    }
}

string YamlScalar(string yaml, string key)
{
    var matches = Regex.Matches(yaml, $@"(?m)^[ \t]*{Regex.Escape(key)}:[ \t]*(?<value>[^\r\n]+)");
    if (matches.Count != 1) throw new InvalidOperationException($"매니페스트의 {key} 항목이 없거나 중복되어 있습니다.");
    return matches[0].Groups["value"].Value.Trim().Trim('\'', '"');
}

void VerifyInstallerManifest(string yaml, GhAsset[] installers)
{
    if (YamlScalar(yaml, "ManifestType") != "installer")
        throw new InvalidOperationException("기존 설치 관리자 매니페스트의 형식이 일치하지 않습니다.");
    var blocks = Regex.Matches(yaml, @"(?ms)^[ \t]*- Architecture:[ \t]*(?<arch>[^\r\n]+)\r?\n(?<body>.*?)(?=^[ \t]*- Architecture:|\z)");
    if (blocks.Count != 2) throw new InvalidOperationException("기존 매니페스트에 두 아키텍처의 설치 관리자가 없습니다.");
    foreach (var (architecture, asset) in new[] { ("x64", installers[0]), ("arm64", installers[1]) })
    {
        var match = blocks.Cast<Match>().Where(block => block.Groups["arch"].Value.Trim().Trim('\'', '"') == architecture).ToArray();
        if (match.Length != 1 || YamlScalar(match[0].Groups["body"].Value, "InstallerUrl") != asset.browser_download_url)
            throw new InvalidOperationException($"기존 매니페스트의 {architecture} 설치 관리자 URL이 릴리스와 일치하지 않습니다.");
        var sha = YamlScalar(match[0].Groups["body"].Value, "InstallerSha256");
        if (!Regex.IsMatch(sha, "\\A[0-9a-fA-F]{64}\\z") ||
            (asset.digest?.StartsWith("sha256:", StringComparison.Ordinal) == true &&
             !sha.Equals(asset.digest[7..], StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"기존 매니페스트의 {architecture} 설치 관리자 SHA-256이 올바르지 않거나 릴리스와 일치하지 않습니다.");
    }
}

async Task SyncWingetFork(HttpClient client, string login)
{
    using var body = new StringContent("{\"branch\":\"master\"}", Encoding.UTF8, "application/json");
    using var response = await client.PostAsync($"repos/{login}/winget-pkgs/merge-upstream", body);
    // A new contributor may not have a fork yet; wingetcreate creates it.
    if (response.StatusCode == HttpStatusCode.NotFound) return;
    CheckResponse(response, "winget-pkgs 포크 동기화");
    WriteLine($"포크 {login}/winget-pkgs의 master를 upstream과 동기화했습니다.");
}

async Task<int> RunSelfTests()
{
    var passed = 0;
    void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Self-test assertion failed.");
    }
    void Test(string name, Action action)
    {
        action(); WriteLine($"PASS {name}"); passed++;
    }
    void Reject(Action action, string expected)
    {
        try { action(); }
        catch (InvalidOperationException exception) when (exception.Message.Contains(expected, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException($"Expected rejection containing '{expected}'.");
    }
    async Task RejectAsync(Func<Task> action, string expected)
    {
        try { await action(); }
        catch (InvalidOperationException exception) when (exception.Message.Contains(expected, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException($"Expected rejection containing '{expected}'.");
    }
    try
    {
        Test("stable tag and Preview exclusion", () =>
        {
            Require(ValidateTag("v1.22.0") == "1.22.0");
            foreach (var tag in new[] { "v1.22.0-preview.12", "v1.22.0-rc.1", "v1.22.0.0", "v1.22.0\n", "" })
                Reject(() => ValidateTag(tag), "RELEASE_TAG");
        });
        Test("verify_only rejects ambiguous input", () =>
        {
            Require(ParseVerifyOnly("true") && !ParseVerifyOnly("false") && !ParseVerifyOnly(null));
            Reject(() => ParseVerifyOnly("yes"), "VERIFY_ONLY");
        });
        Test("RFC 6750 bearer credentials preserve PAT, JWT, and padding", () =>
        {
            foreach (var value in new[] { "ghp_fakePAT_123", "gho_fakeOAuth_123", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJmYWtlIn0.fake_signature-123", "fake-token._~+/ABC123==", "A=" })
            {
                using var client = CreateGitHubClient(value);
                Require(client.DefaultRequestHeaders.Authorization?.Scheme == "Bearer" &&
                    client.DefaultRequestHeaders.Authorization.Parameter == value);
            }
        });
        Test("malformed credentials are never echoed", () =>
        {
            foreach (var value in new[] { "", " fake-secret", "fake-secret ", "fake-secret\tinvalid", "fake-secret\rinvalid", "fake-secret\ninvalid", "fake-secret\r\ninvalid", "fake-secret\0invalid", "fake-secret\u007finvalid", "fake-secret\u0085invalid", "fake-secret\u00a0invalid", "fake-secret가", "fake=secret", "=", "Bearer fake-secret", "fake\"secret" })
            {
                try { using var client = CreateGitHubClient(value); }
                catch (InvalidOperationException exception)
                {
                    Require(!exception.Message.Contains("fake", StringComparison.Ordinal));
                    continue;
                }
                throw new InvalidOperationException("Malformed credential was accepted.");
            }
        });
        Test("credential format errors identify the fixed authentication role", () =>
        {
            foreach (var role in new[] { GitHubCredential.Submission, GitHubCredential.Repository })
            {
                var expected = role == GitHubCredential.Repository ? "GITHUB_TOKEN" : "TABLECLOTH_GITHUB_PAT";
                Reject(() => { using var client = CreateGitHubClient("fake\nsecret", credential: role); }, expected);
            }
        });
        Test("exact package and version boundaries", () =>
        {
            foreach (var title in new[] { "New version: TableClothProject.TableCloth version 1.22.0", "TableClothProject.TableCloth v1.22.0", "tableclothproject.tablecloth V1.22.0" })
                Require(MatchesPackageVersion(title, "1.22.0"));
            foreach (var title in new[] { "TableClothProject.TableCloth 1.22.0.1", "TableClothProject.TableCloth 1.22.0-rc.1", "TableClothProject.TableCloth v1.22.0-rc.1", "TableClothProject.TableCloth 1.22.0+meta", "Other.TableClothProject.TableCloth 1.22.0", "TableClothProject.TableCloth 11.22.0" })
                Require(!MatchesPackageVersion(title, "1.22.0"));
        });
        foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
        {
            using var client = CreateGitHubClient("fake-test-token", new StubHandler(_ => new(status)));
            await RejectAsync(async () => { await ValidateSubmissionCredential(client); }, $"HTTP {(int)status}");
            WriteLine($"PASS authentication HTTP {(int)status}"); passed++;
        }
        foreach (var scopes in new[] { "public_repo", "repo, workflow", "read:user" })
        {
            using var client = CreateGitHubClient("fake-test-token", new StubHandler(_ =>
            {
                var response = JsonResponse("{\"login\":\"test-user\"}");
                response.Headers.Add("X-OAuth-Scopes", scopes);
                return response;
            }));
            if (scopes == "read:user")
                await RejectAsync(async () => { await ValidateSubmissionCredential(client); }, "권한이 없습니다");
            else Require(await ValidateSubmissionCredential(client) == "test-user");
            WriteLine($"PASS submission scope {scopes}"); passed++;
        }

        var assets = new[] { "x64", "arm64" }.Select(architecture =>
        {
            var name = $"TableCloth_1.22.0.0_Release_{architecture}.exe";
            return new GhAsset(name, $"https://github.com/{Repo}/releases/download/v1.22.0/{name}", 123, "sha256:" + new string('a', 64));
        }).ToArray();
        Test("installer identity and official URL", () =>
        {
            Require(SelectInstallers(new("v1.22.0", false, false, assets), "1.22.0").Length == 2);
            Reject(() => SelectInstallers(new("v1.22.0", false, false, [assets[0], assets[0], assets[1]]), "1.22.0"), "고유한");
            Reject(() => SelectInstallers(new("v1.22.0", false, false, [assets[0] with { browser_download_url = "https://example.com/fake.exe" }, assets[1]]), "1.22.0"), "공식 URL");
        });
        var versionYaml = $"PackageIdentifier: {PackageId}\nPackageVersion: 1.22.0\nManifestType: version\n";
        var installerYaml = $"PackageIdentifier: {PackageId}\nPackageVersion: 1.22.0\nManifestType: installer\nInstallers:\n" +
            string.Join("", assets.Select((asset, index) => $"- Architecture: {(index == 0 ? "x64" : "arm64")}\n  InstallerUrl: {asset.browser_download_url}\n  InstallerSha256: {new string('a', 64)}\n"));
        Test("existing installer URL and digest verification", () =>
        {
            VerifyInstallerManifest(installerYaml, assets);
            Reject(() => VerifyInstallerManifest(installerYaml.Replace(new string('a', 64), new string('b', 64)), assets), "SHA-256");
            Reject(() => VerifyInstallerManifest(installerYaml.Replace("Release_x64.exe", "Release_wrong.exe"), assets), "URL");
        });
        foreach (var failure in new[] { "http", "incomplete", "truncated", "missing-fields", "rest-http", "rest-limit" })
        {
            using var client = CreateGitHubClient("fake-test-token", new StubHandler(request =>
            {
                if (request.RequestUri!.AbsolutePath.Contains("/contents/")) return new(HttpStatusCode.NotFound);
                if (failure == "http" || request.RequestUri!.AbsolutePath.StartsWith("/repos/") &&
                    request.RequestUri.AbsolutePath.EndsWith("/issues") && failure == "rest-http")
                    return new(HttpStatusCode.ServiceUnavailable);
                if (failure == "rest-limit" && request.RequestUri!.AbsolutePath.StartsWith("/repos/"))
                    return JsonResponse("[" + string.Join(',', Enumerable.Repeat("{\"number\":1,\"title\":\"Other package\",\"pull_request\":{}}", 100)) + "]");
                if (failure == "missing-fields") return JsonResponse("{}");
                return JsonResponse(failure == "incomplete"
                    ? "{\"total_count\":0,\"incomplete_results\":true,\"items\":[]}"
                    : failure is "rest-http" or "rest-limit" ? "{\"total_count\":0,\"incomplete_results\":false,\"items\":[]}"
                    : "{\"total_count\":1,\"incomplete_results\":false,\"items\":[]}");
            }));
            await RejectAsync(async () => { await FindExistingSubmission(client, "test-user", "1.22.0", assets); },
                failure is "http" or "rest-http" ? "HTTP 503" : failure == "rest-limit" ? "검증 한도" : "불완전");
            WriteLine($"PASS search fail-closed {failure}"); passed++;
        }
        var directory = ManifestPath + "/1.22.0/";
        var files = "[{\"filename\":\"" + directory + PackageId + ".yaml\",\"sha\":\"" + new string('1', 40) + "\",\"status\":\"added\"}," +
            "{\"filename\":\"" + directory + PackageId + ".installer.yaml\",\"sha\":\"" + new string('2', 40) + "\",\"status\":\"added\"}]";
        foreach (var mode in new[] { "upstream", "open-pr", "search-lag", "search-lag-v-prefix", "changed-head", "wrong-version" })
        {
            var reads = 0;
            var pullReads = 0;
            using var client = CreateGitHubClient("fake-test-token", new StubHandler(request =>
            {
                Require(request.Method == HttpMethod.Get);
                reads++;
                var path = request.RequestUri!.AbsolutePath;
                if (path.Contains("/contents/")) return mode == "upstream" ? JsonResponse(files) : new(HttpStatusCode.NotFound);
                var issue = mode == "search-lag-v-prefix"
                    ? "{\"number\":42,\"title\":\"TableClothProject.TableCloth v1.22.0\",\"pull_request\":{}}"
                    : "{\"number\":42,\"title\":\"New version: TableClothProject.TableCloth version 1.22.0\",\"pull_request\":{}}";
                if (path == "/search/issues") return JsonResponse(mode.StartsWith("search-lag", StringComparison.Ordinal)
                    ? "{\"total_count\":0,\"incomplete_results\":false,\"items\":[]}"
                    : "{\"total_count\":1,\"incomplete_results\":false,\"items\":[" + issue + "]}");
                if (path.EndsWith("/issues")) return JsonResponse(mode.StartsWith("search-lag", StringComparison.Ordinal) ? "[" + issue + "]" : "[]");
                if (path.EndsWith("/files")) return JsonResponse(files);
                if (path.Contains("/pulls/"))
                {
                    pullReads++;
                    var sha = new string(mode == "changed-head" && pullReads > 1 ? 'b' : 'a', 40);
                    return JsonResponse("{\"state\":\"open\",\"merged\":false,\"changed_files\":2,\"base\":{\"ref\":\"master\",\"repo\":{\"full_name\":\"microsoft/winget-pkgs\"}},\"head\":{\"sha\":\"" + sha + "\",\"repo\":{\"full_name\":\"test-user/winget-pkgs\"}}}");
                }
                if (path.Contains("/git/blobs/"))
                {
                    var yaml = path.EndsWith(new string('1', 40)) ? versionYaml : installerYaml;
                    if (mode == "wrong-version") yaml = yaml.Replace("PackageVersion: 1.22.0", "PackageVersion: 1.22.0.1");
                    return JsonResponse("{\"encoding\":\"base64\",\"content\":\"" + Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml)) + "\"}");
                }
                throw new InvalidOperationException("Unexpected self-test request.");
            }));
            if (mode is "changed-head" or "wrong-version")
                await RejectAsync(async () => { await FindExistingSubmission(client, "test-user", "1.22.0", assets); }, mode == "changed-head" ? "head SHA" : "버전이 일치하지");
            else Require((await FindExistingSubmission(client, "test-user", "1.22.0", assets))?.Contains(mode == "upstream" ? "/tree/master/" : "/pull/42") == true);
            Require(reads > 0);
            WriteLine($"PASS existing submission {mode}"); passed++;
        }
        WriteLine($"All {passed} offline winget submission tests passed.");
        return 0;
    }
    catch (Exception exception)
    {
        WriteLine($"FAIL: {exception.Message}");
        return 1;
    }
}

HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

internal enum GitHubCredential { Submission, Repository }

internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}

internal sealed record GhRelease(string tag_name, bool draft, bool prerelease, GhAsset[] assets);
internal sealed record GhAsset(string name, string browser_download_url, long size, string? digest);
internal sealed record GhUser(string login);
internal sealed record GhSearch(int? total_count, bool? incomplete_results, GhIssue[]? items);
internal sealed record GhIssue(int number, string title, JsonElement? pull_request);
internal sealed record GhFile(string? path, string? filename, string sha, string? status);
internal sealed record GhBlob(string encoding, string content);
internal sealed record GhRepository(string full_name);
internal sealed record GhRef(string @ref, string sha, GhRepository repo);
internal sealed record GhPull(string state, bool merged, int changed_files, GhRef @base, GhRef head);

[JsonSerializable(typeof(GhRelease))]
[JsonSerializable(typeof(GhUser))]
[JsonSerializable(typeof(GhSearch))]
[JsonSerializable(typeof(GhIssue[]))]
[JsonSerializable(typeof(GhFile[]))]
[JsonSerializable(typeof(GhBlob))]
[JsonSerializable(typeof(GhPull))]
internal partial class GhJson : JsonSerializerContext;

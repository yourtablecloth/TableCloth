#!/usr/bin/env dotnet
#:sdk Cadenza@1.0.15
#:property PublishAot=false

// =============================================================================
// winget-pkgs 자동 제출 스크립트 (TableClothProject.TableCloth)
//
// .github/workflows/winget_publish.yml 가 정식 릴리스(released) 게시 시, 또는
// 수동 dispatch 시 이 .NET 10 file-based app 을 호출한다. 동작 순서:
//   1) RELEASE_TAG 로 지정된 릴리스를 GitHub API 로 조회 (User-Agent 필수)
//   2) draft 면 오류, prerelease 면 정상 건너뜀
//   3) 해당 winget 버전이 이미 등록돼 있으면 멱등적으로 건너뜀
//   4) 릴리스 자산에서 x64 / arm64 설치 관리자(.exe) URL 을 찾고
//   5) Microsoft 공식 wingetcreate 로 매니페스트 업데이트 PR 을 제출
//
// 참고: 현재 winget 매니페스트는 InstallerType: exe 에 x64 + arm64 두 개의
// Installer 노드를 가진다. wingetcreate update 는 --urls 개수가 기존 노드 수와
// 정확히 일치해야 하므로 두 아키텍처 자산이 모두 있어야 한다.
//
// 비밀값: GITHUB_PAT 은 winget-pkgs 포크/PR 권한이 있는 *classic* PAT 이어야
// 하며 public_repo 스코프가 필요하다. fine-grained PAT 은 wingetcreate 가
// 지원하지 않는다. (자세한 설정은 tools/winget/README.md 참고)
// =============================================================================

using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

const string PackageId = "TableClothProject.TableCloth";
const string Repo = "yourtablecloth/TableCloth";                 // 릴리스가 게시되는 저장소
const string WingetRepo = "microsoft/winget-pkgs";
const string ManifestPath = "manifests/t/TableClothProject/TableCloth";
const string WingetCreateUrl = "https://aka.ms/wingetcreate/latest"; // 단독 exe (.NET 6 런타임 필요)

// --- GitHub Actions 로그/요약 헬퍼 (using static System.Console 의 Error 와 충돌하지 않도록 Log* 로 명명) ---
void LogError(string m) => WriteLine($"::error::{m}");
void LogNotice(string m) => WriteLine($"::notice::{m}");
void Summary(string md)
{
    var p = Env.Get("GITHUB_STEP_SUMMARY");
    if (!string.IsNullOrEmpty(p))
        File.AppendAllText(p, md + Environment.NewLine);
}

// --- 1) 입력 검증 ---
var tag = (Env.Get("RELEASE_TAG") ?? string.Empty).Trim();
if (tag.Length == 0)
{
    LogError("RELEASE_TAG 환경 변수가 비어 있습니다. (release 이벤트 또는 workflow_dispatch 입력에서 전달돼야 합니다)");
    return 1;
}
// 릴리스 태그는 항상 3-part(vX.Y.Z) 이다(build.yml 이 Directory.Build.Props 와 일치 검증).
if (!Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$"))
{
    LogError($"릴리스 태그 형식이 올바르지 않습니다: '{tag}' (예상: v1.2.3)");
    return 1;
}

var token = Env.Get("GITHUB_PAT");
if (string.IsNullOrWhiteSpace(token))
{
    LogError("GITHUB_PAT 시크릿이 설정되지 않았습니다. (public_repo 스코프를 가진 classic PAT 필요)");
    return 1;
}

// winget PackageVersion: 'v' 를 떼어 'X.Y.Z' 로 (NormalizeVersion 참고)
var version = NormalizeVersion(tag);
WriteLine($"릴리스 태그 '{tag}' -> winget 버전 '{version}'");

// --- 2) GitHub API 공통 헤더 (User-Agent 없으면 403) ---
Http.Client.DefaultRequestHeaders.UserAgent.ParseAdd("TableCloth-winget-submitter/1.0");
Http.Client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
Http.Client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
// PAT 로 API 읽기를 인증한다. 인증하면 레이트 리밋이 60→5000/hr 로 올라가, IP 를
// 공유하는 GitHub 호스티드 러너에서 흔한 비인증 403 을 방지한다. (wingetcreate
// 다운로드 직전에 다시 제거해 aka.ms 로 토큰이 전송되지 않도록 한다)
Http.Client.DefaultRequestHeaders.Authorization =
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

// --- 3) 릴리스 조회 ---
GhRelease release;
try
{
    release = await Http.GetJson<GhRelease>(
        $"https://api.github.com/repos/{Repo}/releases/tags/{tag}", GhJson.Default);
}
catch (Exception ex)
{
    LogError($"릴리스 '{tag}' 조회에 실패했습니다: {ex.Message}");
    return 1;
}

if (release.draft)
{
    LogError($"릴리스 '{tag}' 가 아직 draft 상태입니다. 게시(publish) 후 다시 시도하세요.");
    return 1;
}
if (release.prerelease)
{
    LogNotice($"릴리스 '{tag}' 는 prerelease 입니다. winget 제출을 건너뜁니다.");
    return 0;
}

// --- 4) 멱등성: 이미 등록된 버전이면 건너뛰기 (조회 불확실 시 fail-closed) ---
bool alreadyExists;
try
{
    alreadyExists = await VersionExistsInWinget(version);
}
catch (Exception ex)
{
    // 404(없음)/200(있음) 외의 응답은 '불확실'로 보고 중복 PR 방지를 위해 중단한다.
    LogError($"winget 버전 존재 여부 확인 실패(중복 제출 방지를 위해 중단): {ex.Message}");
    return 1;
}
if (alreadyExists)
{
    LogNotice($"winget 에 {PackageId} {version} 매니페스트가 이미 존재합니다. 제출을 건너뜁니다.");
    Summary($"### winget 제출 건너뜀\n\n`{PackageId}` **{version}** 매니페스트가 이미 등록돼 있습니다.");
    return 0;
}

// --- 5) 설치 관리자 자산 URL 선택 (x64 / arm64) ---
var assets = release.assets ?? Array.Empty<GhAsset>();
var x64Url = FindInstaller(assets, version, "x64");
var arm64Url = FindInstaller(assets, version, "arm64");

if (x64Url is null || arm64Url is null)
{
    LogError($"릴리스 자산에서 TableCloth_{version}.*_<arch>.exe 설치 관리자를 모두 찾지 못했습니다 " +
             $"(x64={x64Url is not null}, arm64={arm64Url is not null}). " +
             "기존 winget 매니페스트는 두 아키텍처 노드를 모두 요구합니다.");
    foreach (var a in assets)
        WriteLine($"  - 자산: {a.name}");
    return 1;
}

WriteLine($"x64   설치 관리자: {x64Url}");
WriteLine($"arm64 설치 관리자: {arm64Url}");

// --- 5.5) winget-pkgs 포크 동기화 (드리프트 방지) ---
//   wingetcreate 는 PAT 소유자의 winget-pkgs 포크에 PR 브랜치를 만든다. 포크 master
//   가 upstream 보다 크게 뒤처지면(winget-pkgs 는 매우 활발해 금세 수십만 커밋 뒤처짐)
//   제출이 실패할 수 있으므로, 매 제출 전에 merge-upstream 으로 fast-forward 한다.
//   (인증 헤더가 아직 설정돼 있는 이 시점에서 수행. 실패해도 계속 진행한다.)
await SyncWingetFork();

// --- 6) wingetcreate 단독 exe 다운로드 ---
using var tmp = Fs.TempDir();
var exe = Path.Combine(tmp.Path, "wingetcreate.exe");
WriteLine("wingetcreate 다운로드 중...");
// GitHub 토큰을 aka.ms/외부 리다이렉트로 보내지 않도록 인증 헤더 제거.
Http.Client.DefaultRequestHeaders.Authorization = null;
try
{
    await Http.Download(WingetCreateUrl, exe);
}
catch (Exception ex)
{
    LogError($"wingetcreate 다운로드 실패: {ex.Message}");
    return 1;
}

// --- 7) wingetcreate update 실행 ---
//   --urls 는 'URL|architecture' 를 설치 관리자마다 별도 인자로 넘겨야 한다
//   (한 인자에 공백으로 이어붙이면 실패).
//   토큰은 --token(명령줄) 대신 WINGET_CREATE_GITHUB_TOKEN 환경 변수로 전달한다.
//   Microsoft 권장 방식이며 토큰이 프로세스 명령줄/로그에 노출되지 않는다.
//   (Sh.Run 의 자식 프로세스가 현재 프로세스 환경 변수를 그대로 상속한다)
Environment.SetEnvironmentVariable("WINGET_CREATE_GITHUB_TOKEN", token);
var cmd =
    $"\"{exe}\" update {PackageId} --version {version} " +
    $"--urls \"{x64Url}|x64\" \"{arm64Url}|arm64\" " +
    $"--submit";

WriteLine($"실행: wingetcreate update {PackageId} --version {version} --urls (x64, arm64) --submit");
var exit = Sh.Run(cmd);
if (exit != 0)
{
    LogError($"wingetcreate 가 0이 아닌 코드로 종료되었습니다: {exit}");
    Summary($"### ❌ winget 제출 실패\n\n`{PackageId}` **{version}** — wingetcreate 종료 코드 {exit}");
    return exit;
}

LogNotice($"{PackageId} {version} winget 매니페스트 PR 을 제출했습니다.");
Summary(
    $"### ✅ winget 제출 완료\n\n" +
    $"- 패키지: `{PackageId}`\n" +
    $"- 버전: **{version}** (태그 `{tag}`)\n" +
    $"- x64: {x64Url}\n" +
    $"- arm64: {arm64Url}\n\n" +
    $"[microsoft/winget-pkgs PR 확인](https://github.com/{WingetRepo}/pulls?q=is%3Apr+{Uri.EscapeDataString(PackageId)})");
return 0;

// =============================================================================
// 헬퍼
// =============================================================================

// 'vX.Y.Z' -> winget 3-part 버전 'X.Y.Z' ('v' 제거, 만약을 위해 4번째 이후 컴포넌트는 버림)
static string NormalizeVersion(string tag)
{
    var v = tag.TrimStart('v', 'V');
    var parts = v.Split('.');
    return parts.Length > 3 ? string.Join('.', parts[..3]) : v;
}

// 자산 목록에서 해당 버전·아키텍처의 TableCloth 설치 관리자(.exe, Portable 제외) URL 을 찾는다.
static string? FindInstaller(GhAsset[] assets, string version, string arch)
{
    // 파일명은 4-part FileVersion 을 쓰므로(예: TableCloth_1.20.3.0_Release_x64.exe),
    // "TableCloth_<version>." 접두사로 패키지 + 버전을 함께 한정한다. 이렇게 하면
    //   (1) 같은 릴리스에 함께 올라오는 Spork_* 를 잘못 고르는 문제(#395443, #396257)와
    //   (2) 릴리스에 어쩌다 남은 다른 버전의 TableCloth_* 를 고르는 문제
    // 를 원천 차단한다.
    var prefix = $"TableCloth_{version}.";
    foreach (var a in assets)
    {
        var n = a.name;
        if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(a.browser_download_url)) continue;
        if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
        if (!n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
        if (n.Contains("Portable", StringComparison.OrdinalIgnoreCase)) continue;

        if (arch == "x64")
        {
            // x64 매칭 시 arm64 자산을 배제 ("x64" 가 "arm64" 의 부분 문자열은 아니지만 방어적으로)
            if (n.Contains("x64", StringComparison.OrdinalIgnoreCase) &&
                !n.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                return a.browser_download_url;
        }
        else if (n.Contains(arch, StringComparison.OrdinalIgnoreCase))
        {
            return a.browser_download_url;
        }
    }
    return null;
}

// winget-pkgs 에 해당 버전 폴더가 이미 있는지 확인 (멱등성).
//   404 -> 없음(false), 2xx -> 있음(true), 그 외/예외 -> 불확실 → 호출부에서 fail-closed 처리.
async Task<bool> VersionExistsInWinget(string version)
{
    var url = $"https://api.github.com/repos/{WingetRepo}/contents/{ManifestPath}/{version}";
    using var resp = await Http.Client.GetAsync(url);
    if (resp.StatusCode == HttpStatusCode.NotFound) return false;
    if (resp.IsSuccessStatusCode) return true;
    throw new InvalidOperationException(
        $"winget contents API 응답 {(int)resp.StatusCode} {resp.ReasonPhrase}");
}

// PAT 소유자의 winget-pkgs 포크 master 를 upstream(microsoft/winget-pkgs)과
// fast-forward 동기화한다. 포크가 없거나 동기화가 실패해도(경고만) 진행을 막지 않는다.
// (Authorization 헤더가 설정된 상태에서 호출되어야 한다.)
async Task SyncWingetFork()
{
    try
    {
        var me = await Http.GetJson<GhUser>("https://api.github.com/user", GhJson.Default);
        var url = $"https://api.github.com/repos/{me.login}/winget-pkgs/merge-upstream";
        using var body = new StringContent(
            "{\"branch\":\"master\"}", System.Text.Encoding.UTF8, "application/json");
        using var resp = await Http.Client.PostAsync(url, body);
        if (resp.IsSuccessStatusCode)
            WriteLine($"포크 {me.login}/winget-pkgs master 를 upstream 과 동기화했습니다.");
        else
            WriteLine($"::warning::포크 동기화 응답 {(int)resp.StatusCode} {resp.ReasonPhrase} — 계속 진행합니다.");
    }
    catch (Exception ex)
    {
        WriteLine($"::warning::포크 동기화 건너뜀: {ex.Message} — 계속 진행합니다.");
    }
}

// =============================================================================
// GitHub 릴리스 API 응답 (System.Text.Json source-gen; 필요한 필드만 매핑)
// =============================================================================

internal sealed record GhRelease(string tag_name, bool draft, bool prerelease, GhAsset[] assets);
internal sealed record GhAsset(string name, string browser_download_url);
internal sealed record GhUser(string login);

[JsonSerializable(typeof(GhRelease))]
[JsonSerializable(typeof(GhUser))]
internal partial class GhJson : JsonSerializerContext;

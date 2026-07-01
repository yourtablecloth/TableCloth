using Spork.Browsers;
using Spork.Components;
using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    /// <summary>
    /// 드물게 Windows Sandbox 기본 이미지에 Microsoft Edge(msedge.exe)가 없는 경우를 대비한 복구 단계.
    /// Edge가 있으면 아무 동작도 하지 않고(Evaluate=false → skip), 없을 때만 Microsoft Edge Enterprise MSI를
    /// 내려받아 무인 설치해 이후 Edge 의존 단계(정책/확장/실행/사이트 열기)가 정상 동작하도록 한다.
    /// (이슈 #184: 원인은 Windows Sandbox 쪽 Edge 부재이지만, TableCloth 는 크래시 대신 복구를 시도한다.)
    /// </summary>
    /// <remarks>
    /// 다운로드 URL 은 아키텍처별로 다르며 버전에 따라 바뀐다. x64 는 안정적인 fwlink 가 있어 폴백으로 쓰고,
    /// 그 외(arm64 등)는 Edge for Business API 로 현재 Stable MSI 위치를 조회한다. WebView2 런타임은
    /// 브라우저(msedge.exe)를 복원하지 못하므로 사용하지 않고, 반드시 전체 Edge(Enterprise MSI)를 설치한다.
    /// </remarks>
    public sealed class EnsureMicrosoftEdgeStep : StepBase<InstallItemViewModel>
    {
        public EnsureMicrosoftEdgeStep(
            ISharedLocations sharedLocations,
            IHttpClientFactory httpClientFactory,
            IWebBrowserServiceFactory webBrowserServiceFactory,
            IAppMessageBox appMessageBox)
        {
            _sharedLocations = sharedLocations;
            _httpClientFactory = httpClientFactory;
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _appMessageBox = appMessageBox;
        }

        private readonly ISharedLocations _sharedLocations;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IAppMessageBox _appMessageBox;

        // 아키텍처별 현재 Stable Edge 설치 관리자 위치를 알려주는 공개 API.
        private const string EdgeEnterpriseApiUrl = "https://edgeupdates.microsoft.com/api/products?view=enterprise";
        // x64 전용 안정 fwlink. API 조회 실패 시 폴백으로 사용한다(MicrosoftEdgeEnterpriseX64.msi 로 리다이렉트).
        private const string EdgeEnterpriseX64FwLink = "https://go.microsoft.com/fwlink/?LinkID=2093437";

        // LoadContent → Play 로 다운로드 경로를 전달. 본 스텝은 설치 흐름당 1회만 순차 실행되므로 필드로 충분.
        private string _downloadedMsiPath;

        private bool IsEdgePresent()
            => _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService()
                .TryGetBrowserExecutablePath(out _);

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel _, CancellationToken cancellationToken = default)
            // Edge 가 없을 때만 실행한다. 정상 샌드박스(Edge 존재)에서는 skip 되어 아무 영향이 없다.
            => Task.FromResult(!IsEdgePresent());

        public override async Task LoadContentForStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            _downloadedMsiPath = null;

            try
            {
                var url = await ResolveEdgeMsiUrlAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(url))
                    return; // 위치를 못 구하면 Play 에서 안내로 처리

                var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();
                var tempFilePath = Path.Combine(downloadFolderPath, $"edge_{Guid.NewGuid():n}.msi");

                var httpClient = _httpClientFactory.CreateGoogleChromeMimickedHttpClient();
                using (var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var remoteStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using (var fileStream = File.OpenWrite(tempFilePath))
                    {
                        await remoteStream.CopyStreamWithProgressAsync(
                            fileStream, new Progress<double>(progressCallback), 81920, cancellationToken).ConfigureAwait(false);
                    }
                }

                _downloadedMsiPath = tempFilePath;
            }
            catch
            {
                // 다운로드 실패(네트워크 등)는 크래시로 이어지지 않게 삼키고, Play 에서 안내로 처리한다.
                _downloadedMsiPath = null;
            }
        }

        public override async Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            progressCallback?.Invoke(5d);

            if (string.IsNullOrWhiteSpace(_downloadedMsiPath) || !File.Exists(_downloadedMsiPath))
            {
                ShowGuidance();
                progressCallback?.Invoke(100d);
                return;
            }

            try
            {
                var msiexecPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
                var psi = new ProcessStartInfo(msiexecPath, $"/i \"{_downloadedMsiPath}\" /qn /norestart")
                {
                    UseShellExecute = false,
                };

                var cpSource = new TaskCompletionSource<int>();
                using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
                {
                    process.Exited += (sender, _e) => cpSource.TrySetResult(((Process)sender).ExitCode);

                    if (!process.Start())
                    {
                        ShowGuidance();
                        return;
                    }

                    if (process.HasExited)
                        cpSource.TrySetResult(process.ExitCode);

                    using (cancellationToken.Register(() => cpSource.TrySetCanceled(cancellationToken)))
                        await cpSource.Task.ConfigureAwait(false);
                }

                progressCallback?.Invoke(90d);

                // 설치했는데도 여전히 Edge 를 못 찾으면(설치 실패) 사용자에게 안내한다.
                if (!IsEdgePresent())
                    ShowGuidance();
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, false);
                ShowGuidance();
            }
            finally
            {
                try { File.Delete(_downloadedMsiPath); } catch { /* 임시 파일 정리 실패는 무시 */ }
                progressCallback?.Invoke(100d);
            }
        }

        private void ShowGuidance()
            => _appMessageBox.DisplayError(UIStringResources.Sandbox_EdgeMissing_Guidance, false);

        /// <summary>
        /// 현재 아키텍처의 Stable Edge Enterprise MSI URL 을 구한다. x64 는 안정 fwlink 를 우선 쓰고,
        /// 그 외는 Edge for Business API 에서 조회한다. 실패 시 x64 fwlink 로 폴백(그 외는 null).
        /// </summary>
        private async Task<string> ResolveEdgeMsiUrlAsync(CancellationToken cancellationToken)
        {
            var arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                _ => "x64",
            };

            if (string.Equals(arch, "x64", StringComparison.OrdinalIgnoreCase))
                return EdgeEnterpriseX64FwLink;

            try
            {
                var httpClient = _httpClientFactory.CreateGoogleChromeMimickedHttpClient();
                using var response = await httpClient.GetAsync(EdgeEnterpriseApiUrl, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var doc = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

                foreach (var product in doc.RootElement.EnumerateArray())
                {
                    if (!product.TryGetProperty("Product", out var productName) ||
                        !string.Equals(productName.GetString(), "Stable", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!product.TryGetProperty("Releases", out var releases))
                        continue;

                    foreach (var release in releases.EnumerateArray())
                    {
                        if (!release.TryGetProperty("Platform", out var platform) ||
                            !string.Equals(platform.GetString(), "Windows", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!release.TryGetProperty("Architecture", out var architecture) ||
                            !string.Equals(architecture.GetString(), arch, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!release.TryGetProperty("Artifacts", out var artifacts))
                            continue;

                        foreach (var artifact in artifacts.EnumerateArray())
                        {
                            if (artifact.TryGetProperty("ArtifactName", out var artifactName) &&
                                string.Equals(artifactName.GetString(), "msi", StringComparison.OrdinalIgnoreCase) &&
                                artifact.TryGetProperty("Location", out var location))
                                return location.GetString();
                        }
                    }
                }
            }
            catch
            {
                // 조회 실패는 무시하고 폴백으로 넘어간다.
            }

            return null;
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}

using Spork.Browsers;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Components.Implementations
{
    /// <summary>
    /// <see cref="IMicrosoftEdgeInstaller"/>의 기본 구현. 다운로드 URL 은 아키텍처별로 다르고 버전에 따라
    /// 바뀐다. x64 는 안정적인 fwlink 를 폴백으로 쓰고, 그 외(arm64 등)는 Edge for Business API 로
    /// 현재 Stable MSI 위치를 조회한다.
    /// </summary>
    public sealed class MicrosoftEdgeInstaller : IMicrosoftEdgeInstaller
    {
        public MicrosoftEdgeInstaller(
            ISharedLocations sharedLocations,
            IHttpClientFactory httpClientFactory,
            IWebBrowserServiceFactory webBrowserServiceFactory)
        {
            _sharedLocations = sharedLocations;
            _httpClientFactory = httpClientFactory;
            _webBrowserServiceFactory = webBrowserServiceFactory;
        }

        private readonly ISharedLocations _sharedLocations;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;

        // 아키텍처별 현재 Stable Edge 설치 관리자 위치를 알려주는 공개 API.
        private const string EdgeEnterpriseApiUrl = "https://edgeupdates.microsoft.com/api/products?view=enterprise";
        // x64 전용 안정 fwlink. API 조회 실패 시 폴백으로 사용한다(MicrosoftEdgeEnterpriseX64.msi 로 리다이렉트).
        private const string EdgeEnterpriseX64FwLink = "https://go.microsoft.com/fwlink/?LinkID=2093437";

        public bool IsEdgeInstalled()
            => _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService()
                .TryGetBrowserExecutablePath(out _);

        public async Task<bool> InstallOrRepairAsync(IProgress<double> progress = null, CancellationToken cancellationToken = default)
        {
            var msiPath = await DownloadEdgeMsiAsync(progress, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(msiPath) || !File.Exists(msiPath))
                return false;

            try
            {
                await RunMsiInstallAsync(msiPath, cancellationToken).ConfigureAwait(false);
                return IsEdgeInstalled();
            }
            finally
            {
                try { File.Delete(msiPath); } catch { /* 임시 파일 정리 실패는 무시 */ }
            }
        }

        /// <summary>현재 아키텍처의 Edge MSI 를 다운로드 폴더에 내려받고 그 경로를 반환한다. 실패 시 null.</summary>
        private async Task<string> DownloadEdgeMsiAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            var url = await ResolveEdgeMsiUrlAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(url))
                return null;

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
                        fileStream, progress, 81920, cancellationToken).ConfigureAwait(false);
                }
            }

            return tempFilePath;
        }

        /// <summary>msiexec 로 무인 설치를 실행하고 종료를 기다린다.</summary>
        private static async Task RunMsiInstallAsync(string msiPath, CancellationToken cancellationToken)
        {
            var msiexecPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "msiexec.exe");
            var psi = new System.Diagnostics.ProcessStartInfo(msiexecPath, $"/i \"{msiPath}\" /qn /norestart")
            {
                UseShellExecute = false,
            };

            var cpSource = new TaskCompletionSource<int>();
            using (var process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                process.Exited += (sender, _e) => cpSource.TrySetResult(((System.Diagnostics.Process)sender).ExitCode);

                if (!process.Start())
                    return;

                if (process.HasExited)
                    cpSource.TrySetResult(process.ExitCode);

                using (cancellationToken.Register(() => cpSource.TrySetCanceled(cancellationToken)))
                    await cpSource.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 현재 아키텍처의 Stable Edge Enterprise MSI URL 을 구한다. x64 는 안정 fwlink 를 우선 쓰고,
        /// 그 외는 Edge for Business API 에서 조회한다. 실패 시 null.
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
                // 조회 실패는 무시하고 null 로 넘긴다(호출부가 안내 처리).
            }

            return null;
        }
    }
}

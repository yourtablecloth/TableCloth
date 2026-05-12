using Spork.Components;
using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    public sealed class PackageInstallStep : StepBase<PackageInstallItemViewModel>
    {
        public PackageInstallStep(
            ISharedLocations sharedLocations,
            IHttpClientFactory httpClientFactory)
        {
            _sharedLocations = sharedLocations;
            _httpClientFactory = httpClientFactory;
        }

        private ISharedLocations _sharedLocations;
        private IHttpClientFactory _httpClientFactory;

        public override Task<bool> EvaluateRequiredStepAsync(PackageInstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public override async Task LoadContentForStepAsync(PackageInstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();

            // URL에서 파일 확장자를 추출하여 적절한 확장자 사용
            var extension = GetFileExtensionFromUrl(viewModel.PackageUrl);
            var tempFileName = $"installer_{Guid.NewGuid():n}{extension}";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            viewModel.DownloadedFilePath = tempFilePath;

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            var httpClient = _httpClientFactory.CreateGoogleChromeMimickedHttpClient();

            // Note: HttpClient.GetStreamAsync로는 Content-Length 헤더 값을 스트림 길이로 받지 못함.
            using (var response = await httpClient.GetAsync(viewModel.PackageUrl, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                var remoteStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using (var fileStream = File.OpenWrite(tempFilePath))
                {
                    await remoteStream.CopyStreamWithProgressAsync(
                        fileStream, new Progress<double>(progressCallback),
                        81920, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public override async Task PlayStepAsync(PackageInstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            var tempFilePath = viewModel.DownloadedFilePath;
            var extension = Path.GetExtension(tempFilePath);

            ProcessStartInfo psi;

            // MSI 파일인 경우 msiexec.exe를 통해 설치
            if (string.Equals(extension, ".msi", StringComparison.OrdinalIgnoreCase))
            {
                var msiexecPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "msiexec.exe");

                // msiexec /i "패키지경로" [추가 인수]
                var arguments = $"/i \"{tempFilePath}\"";
                if (!string.IsNullOrWhiteSpace(viewModel.Arguments))
                    arguments += $" {viewModel.Arguments}";

                psi = new ProcessStartInfo(msiexecPath, arguments)
                {
                    UseShellExecute = false,
                };
            }
            else
            {
                // EXE 파일 또는 기타 실행 파일은 직접 실행
                psi = new ProcessStartInfo(tempFilePath, viewModel.Arguments)
                {
                    UseShellExecute = false,
                };
            }

            var cpSource = new TaskCompletionSource<int>();
            using (var process = new Process() { StartInfo = psi, })
            {
                process.EnableRaisingEvents = true;
                process.Exited += (_sender, _e) =>
                {
                    var realSender = _sender as Process;
                    cpSource.TrySetResult(realSender.ExitCode);
                };

                if (!process.Start())
                    TableClothAppException.Throw(ErrorStrings.Error_Package_CanNotStart);

                // 프로세스가 이미 종료된 경우를 처리 (레이스 컨디션 방지)
                if (process.HasExited)
                    cpSource.TrySetResult(process.ExitCode);

                using (cancellationToken.Register(() => cpSource.TrySetCanceled(cancellationToken)))
                {
                    await cpSource.Task.ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// URL에서 파일 확장자를 추출합니다.
        /// </summary>
        /// <param name="url">패키지 다운로드 URL</param>
        /// <returns>파일 확장자 (기본값: .exe)</returns>
        private static string GetFileExtensionFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return ".exe";

            try
            {
                var uri = new Uri(url, UriKind.Absolute);
                var path = uri.AbsolutePath;

                // 쿼리 문자열 제거
                var queryIndex = path.IndexOf('?');
                if (queryIndex >= 0)
                    path = path.Substring(0, queryIndex);

                var extension = Path.GetExtension(path);

                // 지원하는 확장자인 경우 해당 확장자 반환
                if (string.Equals(extension, ".msi", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return extension.ToLowerInvariant();
                }
            }
            catch
            {
                // URL 파싱 실패 시 기본값 사용
            }

            return ".exe";
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}

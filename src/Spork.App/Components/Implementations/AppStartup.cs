using Microsoft.Extensions.Logging;
using Spork.Browsers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Models;
using TableCloth.Resources;

namespace Spork.Components.Implementations
{
    public sealed class AppStartup : IAppStartup
    {
        public AppStartup(
            IAppMessageBox appMessageBox,
            ICommandLineArguments commandLineArguments,
            IResourceCacheManager resourceCacheManager,
            IWebBrowserServiceFactory webBrowserServiceFactory,
            IShortcutCreator shortcutCreator,
            ISharedLocations sharedLocations,
            ISandboxBootstrap sandboxBootstrap,
            ILogger<AppStartup> logger)
        {
            _appMessageBox = appMessageBox;
            _commandLineArguments = commandLineArguments;
            _resourceCacheManager = resourceCacheManager;
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _defaultWebBrowserService = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
            _mutex = new Mutex(true, $"Global\\{GetType().FullName}", out this._isFirstInstance);
            _shortcutCreator = shortcutCreator;
            _sharedLocations = sharedLocations;
            _sandboxBootstrap = sandboxBootstrap;
            _logger = logger;
        }

        private readonly IAppMessageBox _appMessageBox;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IWebBrowserService _defaultWebBrowserService;
        private readonly IShortcutCreator _shortcutCreator;
        private readonly ISharedLocations _sharedLocations;
        private readonly ISandboxBootstrap _sandboxBootstrap;
        private readonly ILogger _logger;

        private bool _disposed;
        private readonly Mutex _mutex;
        private readonly bool _isFirstInstance;

        ~AppStartup() => Dispose(false);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_mutex != null)
                {
                    // ReleaseMutex는 두 가지 조건이 모두 만족되어야 안전하다.
                    // (1) 현재 인스턴스가 실제로 mutex 소유자여야 한다.
                    //     두 번째 동시 실행 인스턴스(_isFirstInstance == false)는 소유권이 없다.
                    // (2) ReleaseMutex 호출 스레드 == 소유권을 획득한 스레드여야 한다.
                    //     DI 컨테이너의 DisposeAsync는 별도 스레드에서 호출될 수 있어 어긋날 수 있다.
                    // 어느 한쪽이라도 위반되면 ApplicationException이 발생하므로 best-effort로 처리한다.
                    if (_isFirstInstance)
                    {
                        try { _mutex.ReleaseMutex(); }
                        catch (ApplicationException) { /* 소유권 없음 / 스레드 미스매치 */ }
                        catch (ObjectDisposedException) { /* 이미 dispose 됨 */ }
                    }

                    try { _mutex.Dispose(); }
                    catch (Exception) { /* dispose 단계의 예외가 종료 흐름을 막지 않도록 흡수 */ }
                }
            }

            _disposed = true;
        }

        public async Task<ApplicationStartupResultModel> HasRequirementsMetAsync(IList<string> warnings, CancellationToken cancellationToken = default)
        {
            var result = default(ApplicationStartupResultModel);

            if (!this._isFirstInstance)
            {
                result = ApplicationStartupResultModel.FromErrorMessage(
                    ErrorStrings.Error_Already_TableCloth_Running, isCritical: true, providedWarnings: warnings);
                return result;
            }

            result = ApplicationStartupResultModel.FromSucceedResult(providedWarnings: warnings);
            return await Task.FromResult(result).ConfigureAwait(false);
        }

        public async Task<ApplicationStartupResultModel> InitializeAsync(
            IList<string> warnings,
            CancellationToken cancellationToken = default)
        {
            var result = default(ApplicationStartupResultModel);
            var parsedArgs = _commandLineArguments.GetCurrent();

            // net48 시절에는 ServicePointManager.ServerCertificateValidationCallback이 HttpClient에도
            // 적용됐지만 .NET Core/5+ 이후로는 HttpClient가 SocketsHttpHandler를 거치므로 이 콜백이 무시된다.
            // 따라서 본 라인은 .NET 10에서 죽은 코드이고, 같은 보호를 회복하려면 AddHttpClient에서
            // HttpClientHandler.ServerCertificateCustomValidationCallback을 직접 구성해야 한다.
            // 우선 무동작 코드를 제거하고, 필요해질 때 HttpClient 측에 cert 검증 정책을 추가하는 식으로 분리한다.

            // StartupScript.cmd에서 PowerShell로 처리하던 부팅 준비 작업(DNS 설정, 인증서 NPKI 배치,
            // NPKI 마운트 사본화)을 .NET 측으로 옮긴 진입점. PowerShell 콜드 스타트 수 초를 절약한다.
            // 모든 단계는 best-effort라 카탈로그 로드를 막지 않는다.
            await _sandboxBootstrap.RunAsync(cancellationToken).ConfigureAwait(false);

            const int retryCount = 3;

            try
            {
                for (int attemptCount = 1; attemptCount <= retryCount; attemptCount++)
                {
                    try
                    {
                        var document = await _resourceCacheManager.LoadCatalogDocumentAsync(cancellationToken).ConfigureAwait(false);
                        document.EnsureNotNull(ErrorStrings.Error_CatalogDeserilizationFailure);
                    }
                    catch (Exception ex)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1.5d * attemptCount), cancellationToken).ConfigureAwait(false);

                        if (attemptCount == retryCount)
                        {
                            result = ApplicationStartupResultModel.FromErrorMessage(
                                StringResources.Error_With_Exception(ErrorStrings.Error_CatalogLoadFailure, ex), ex,
                                isCritical: true, providedWarnings: warnings);
                            return result;
                        }

                        continue;
                    }

                    break;
                }

                // SelectedServices가 비어 있어도 종료하지 않는다.
                // MainWindow가 카탈로그 모드로 진입하여 사용자가 직접 사이트를 선택하도록 한다.
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, true);
                return ApplicationStartupResultModel.FromException(ex, isCritical: true, providedWarnings: warnings);
            }

            // 바탕화면 시그니처는 wsb 부팅 시점에 1회만 적용한다 (이전에는 사이트 설치 단계의
            // 매 실행마다 같은 작업을 반복했음). 실패해도 카탈로그 진입은 막지 않는다.
            TrySetSignatureWallpaper();

            result = ApplicationStartupResultModel.FromSucceedResult(providedWarnings: warnings);
            return await Task.FromResult(result).ConfigureAwait(false);
        }

        private void TrySetSignatureWallpaper()
        {
            try
            {
                var picturesDirectoryPath = _sharedLocations.GetPicturesDirectoryPath();

                if (!Directory.Exists(picturesDirectoryPath))
                    Directory.CreateDirectory(picturesDirectoryPath);

                var wallpaperPath = Path.Combine(picturesDirectoryPath, "Signature.jpg");
                Properties.Resources.Signature.Save(wallpaperPath, ImageFormat.Jpeg);

                var result = NativeMethods.SystemParametersInfoW(
                    NativeMethods.SetDesktopWallpaper, 0, wallpaperPath,
                    NativeMethods.UpdateIniFile | NativeMethods.SendWinIniChange);

                if (result == 0)
                {
                    var lastWin32Error = Marshal.GetLastWin32Error();
                    _logger?.LogWarning(
                        "SetDesktopWallpaper failed. SystemParametersInfoW says: {result} and GetLastWin32Error says: {lastWin32Error}",
                        result, lastWin32Error);
                }

                NativeMethods.UpdatePerUserSystemParameters(IntPtr.Zero, IntPtr.Zero, "1, True", 0);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to set signature wallpaper at sandbox boot");
            }
        }

    }
}

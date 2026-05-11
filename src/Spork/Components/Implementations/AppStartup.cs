using Spork.Browsers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
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
            ISharedLocations sharedLocations)
        {
            _appMessageBox = appMessageBox;
            _commandLineArguments = commandLineArguments;
            _resourceCacheManager = resourceCacheManager;
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _defaultWebBrowserService = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
            _mutex = new Mutex(true, $"Global\\{GetType().FullName}", out this._isFirstInstance);
            _shortcutCreator = shortcutCreator;
            _sharedLocations = sharedLocations;
        }

        private readonly IAppMessageBox _appMessageBox;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IWebBrowserService _defaultWebBrowserService;
        private readonly IShortcutCreator _shortcutCreator;
        private readonly ISharedLocations _sharedLocations;

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
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;

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

            result = ApplicationStartupResultModel.FromSucceedResult(providedWarnings: warnings);
            return await Task.FromResult(result).ConfigureAwait(false);
        }

        private bool ValidateRemoteCertificate(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
            // If the certificate is a valid, signed certificate, return true.
            if (error == SslPolicyErrors.None)
                return true;

            _appMessageBox.DisplayError(StringResources.Error_X509CertError(cert.Subject, error.ToString()), false);
            return false;
        }
    }
}

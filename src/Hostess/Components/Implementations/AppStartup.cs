using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Models;
using TableCloth.Resources;

namespace Hostess.Components.Implementations
{
    public sealed class AppStartup : IAppStartup
    {
        public AppStartup(
            IAppMessageBox appMessageBox,
            IAppUserInterface appUserInterface,
            ICommandLineArguments commandLineArguments,
            IResourceCacheManager resourceCacheManager)
        {
            _appMessageBox = appMessageBox;
            _appUserInterface = appUserInterface;
            _commandLineArguments = commandLineArguments;
            _resourceCacheManager = resourceCacheManager;
            _mutex = new Mutex(true, $"Global\\{GetType().FullName}", out this._isFirstInstance);
        }

        private readonly IAppMessageBox _appMessageBox;
        private readonly IAppUserInterface _appUserInterface;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IResourceCacheManager _resourceCacheManager;

        private bool _disposed;
        private readonly Mutex _mutex;
        private readonly bool _isFirstInstance;

        ~AppStartup() => Dispose(false);

        public void Dispose() => Dispose(true);

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_mutex != null)
                {
                    _mutex.ReleaseMutex();
                    _mutex.Dispose();
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
            var parsedArgs = _commandLineArguments.Current;
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;

            const int retryCount = 3;

            try
            {
                for (int attemptCount = 1; attemptCount <= retryCount; attemptCount++)
                {
                    try
                    {
                        var document = await _resourceCacheManager.LoadCatalogDocumentAsync(cancellationToken) ??
                            throw new XmlException(ErrorStrings.Error_CatalogDeserilizationFailure);
                    }
                    catch (Exception ex)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1.5d * attemptCount), cancellationToken).ConfigureAwait(false);

                        if (attemptCount == retryCount)
                        {
                            result = ApplicationStartupResultModel.FromErrorMessage(
                                StringResources.Error_CatalogLoadFailure(ex), ex,
                                isCritical: true, providedWarnings: warnings);
                            return result;
                        }

                        continue;
                    }

                    break;
                }

                if (!parsedArgs.SelectedServices.Any())
                {
                    _appMessageBox.DisplayInfo(UIStringResources.Hostess_No_Targets);

                    Process.Start(new ProcessStartInfo(CommonStrings.UnboundHomeUrl)
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Maximized,
                    });

                    result = ApplicationStartupResultModel.FromHaltedResult(providedWarnings: warnings);
                    return result;
                }
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, true);
                return ApplicationStartupResultModel.FromException(ex, isCritical: true, providedWarnings: warnings);
            }

            result = ApplicationStartupResultModel.FromSucceedResult(providedWarnings: warnings);
            return await Task.FromResult(result).ConfigureAwait(false);
        }

        private T DeserializeFromXml<T>(Stream readableStream)
            where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            var xmlReaderSetting = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
            };

            using (var contentStream = XmlReader.Create(readableStream, xmlReaderSetting))
            {
                return (T)serializer.Deserialize(contentStream);
            }
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

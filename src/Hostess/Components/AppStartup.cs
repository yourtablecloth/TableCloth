using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Hostess.Components
{
    public sealed class AppStartup : IDisposable
    {
        public AppStartup(
            AppMessageBox appMessageBox,
            SharedProperties sharedProperties,
            AppUserInterface appUserInterface)
        {
            _appMessageBox = appMessageBox;
            _sharedProperties = sharedProperties;
            _appUserInterface = appUserInterface;
            _mutex = new Mutex(true, $"Global\\{GetType().FullName}", out this._isFirstInstance);
        }

        private readonly AppMessageBox _appMessageBox;
        private readonly SharedProperties _sharedProperties;
        private readonly AppUserInterface _appUserInterface;

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

        public bool HasRequirementsMet(IList<string> warnings, out Exception failedReason, out bool isCritical)
        {
            if (!this._isFirstInstance)
            {
                failedReason = new ApplicationException(StringResources.Error_Already_TableCloth_Running);
                isCritical = true;
                return false;
            }

            failedReason = null;
            isCritical = false;
            return true;
        }

        public bool Initialize(out Exception failedReason, out bool isCritical)
        {
            var parsedArgs = CommandLineArgumentModel.ParseFromArgv();
            ServicePointManager.ServerCertificateValidationCallback += ValidateRemoteCertificate;

            const int retryCount = 3;

            try
            {
                for (int attemptCount = 1; attemptCount <= retryCount; attemptCount++)
                {
                    CatalogDocument catalog = null;
                    string lastModifiedValue = null;

                    try
                    {
                        using (var webClient = new WebClient())
                        using (var catalogStream = webClient.OpenRead(StringResources.CatalogUrl))
                        {
                            lastModifiedValue = webClient.ResponseHeaders.Get("Last-Modified");
                            catalog = DeserializeFromXml<CatalogDocument>(catalogStream);

                            if (catalog == null)
                            {
                                failedReason = new XmlException(StringResources.HostessError_CatalogDeserilizationFailure);
                                isCritical = true;
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        catalog = null;
                        Thread.Sleep(TimeSpan.FromSeconds(1.5d * attemptCount));

                        if (attemptCount == retryCount)
                        {
                            _appMessageBox.DisplayError(StringResources.HostessError_CatalogLoadFailure(ex), true);
                            failedReason = ex;
                            isCritical = true;
                            return false;
                        }

                        continue;
                    }

                    _sharedProperties.InitCatalogDocument(catalog);
                    _sharedProperties.InitCatalogLastModified(lastModifiedValue);
                    break;
                }

                if (!parsedArgs.SelectedServices.Any())
                {
                    _appMessageBox.DisplayInfo(StringResources.Hostess_No_Targets);

                    Process.Start(new ProcessStartInfo("https://www.naver.com/")
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Maximized,
                    });

                    failedReason = null;
                    isCritical = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, true);
            }

            failedReason = null;
            isCritical = false;
            return true;
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

            _appMessageBox.DisplayError(StringResources.HostessError_X509CertError(cert, error), false);
            return false;
        }
    }
}

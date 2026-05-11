using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using TableCloth;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Spork.Components.Implementations
{
    public sealed class ResourceCacheManager : IResourceCacheManager
    {
        public ResourceCacheManager(
            IResourceResolver resourceResolver,
            ILogger<ResourceCacheManager> logger)
        {
            _resourceResolver = resourceResolver;
            _logger = logger;
        }

        private readonly IResourceResolver _resourceResolver;
        private readonly ILogger _logger;

        private CatalogDocument _catalogDocument;

        public async Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
        {
            // 1차: 네트워크에서 카탈로그 로드
            try
            {
                var doc = await _resourceResolver.DeserializeCatalogAsync(cancellationToken).ConfigureAwait(false);

                if (doc.Result != null)
                {
                    _catalogDocument = doc.Result;
                    return _catalogDocument;
                }

                _logger.LogWarning("Catalog network response empty; will try fallback snapshot");
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogWarning(ex, "Catalog network load failed; will try fallback snapshot");
            }

            // 2차: 호스트가 staging에 주입해 둔 로컬 스냅샷 (Spork.exe와 동일 폴더의 catalog\\catalog.xml)
            // 샌드박스 내부 네트워크가 막혀 있어도 호스트가 갖고 있는 직전 카탈로그로 동작 가능하도록 한다.
            var snapshot = TryLoadSnapshot();
            if (snapshot != null)
            {
                _logger.LogInformation("Loaded catalog from host-injected snapshot: {path}", GetSnapshotFilePath());
                _catalogDocument = snapshot;
                return _catalogDocument;
            }

            TableClothAppException.Throw("Cannot load catalog document from remote source or local snapshot.");
            return null; // unreachable
        }

        private CatalogDocument TryLoadSnapshot()
        {
            try
            {
                var path = GetSnapshotFilePath();

                if (!File.Exists(path))
                    return null;

                var xmlReaderSettings = new XmlReaderSettings
                {
                    XmlResolver = null,
                    DtdProcessing = DtdProcessing.Prohibit,
                };

                using (var stream = File.OpenRead(path))
                using (var xmlReader = XmlReader.Create(stream, xmlReaderSettings))
                {
                    return XmlCatalogParser.ParseCatalogDocument(xmlReader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalog snapshot load failed");
                return null;
            }
        }

        private static string GetSnapshotFilePath()
        {
            var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            return Path.Combine(exeDirectory, "catalog", "catalog.xml");
        }

        public CatalogDocument CatalogDocument
            => _catalogDocument.EnsureNotNull(StringResources.Error_With_Exception(ErrorStrings.Error_CatalogLoadFailure, null));
    }
}

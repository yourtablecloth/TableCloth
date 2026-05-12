using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class ResourceCacheManager(
    ISharedLocations sharedLocations,
    IResourceResolver resourceResolver,
    ICatalogDeserializer catalogDeserializer,
    ILogger<ResourceCacheManager> logger) : IResourceCacheManager
{
    private const int MaxRetryCount = 3;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(1.5);

    private CatalogDocument? _catalogDocument;
    private readonly Dictionary<string, ImageSource> _imageTable = [];

    public async Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        // 원격 소스에서 카탈로그 로드 시도 (재시도 로직 포함)
        for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
        {
            try
            {
                var doc = await resourceResolver.DeserializeCatalogAsync(cancellationToken).ConfigureAwait(false);

                if (doc.ThrownException != null)
                {
                    lastException = doc.ThrownException;
                    logger.LogWarning(doc.ThrownException, 
                        "Failed to load catalog from remote source (attempt {Attempt}/{MaxRetry})", 
                        attempt, MaxRetryCount);
                }
                else if (doc.Result != null)
                {
                    _catalogDocument = doc.Result;

                    // 성공 시 로컬 캐시에 저장
                    // await SaveCatalogCacheAsync(cancellationToken).ConfigureAwait(false);

                    return _catalogDocument;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                logger.LogWarning(ex, 
                    "Exception during catalog load from remote source (attempt {Attempt}/{MaxRetry})", 
                    attempt, MaxRetryCount);
            }

            if (attempt < MaxRetryCount)
            {
                var delay = TimeSpan.FromTicks(BaseRetryDelay.Ticks * attempt);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        // 원격 소스 실패 시 로컬 캐시에서 로드 시도
        logger.LogInformation("Attempting to load catalog from local cache");
        var cachedDocument = await LoadCatalogCacheAsync(cancellationToken).ConfigureAwait(false);

        if (cachedDocument != null)
        {
            _catalogDocument = cachedDocument;
            logger.LogInformation("Successfully loaded catalog from local cache");
            return _catalogDocument;
        }

        // 모든 시도 실패
        var errorMessage = StringResources.Error_With_Exception(ErrorStrings.Error_CatalogLoadFailure, lastException);
        throw new InvalidOperationException(errorMessage, lastException);
    }

    private async Task<CatalogDocument?> LoadCatalogCacheAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = sharedLocations.CatalogCacheFilePath;

            if (!File.Exists(cachePath))
            {
                logger.LogDebug("Catalog cache file not found at {CachePath}", cachePath);
                return null;
            }

            using var stream = File.OpenRead(cachePath);
            var document = catalogDeserializer.Deserialize(stream, new UTF8Encoding(false));

            if (document != null)
            {
                logger.LogDebug("Catalog cache loaded from {CachePath}", cachePath);
            }

            return document;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load catalog cache");
            return null;
        }
    }

    public async Task LoadSiteImagesAsync(CancellationToken cancellationToken = default)
    {
        var services = CatalogDocument.Services;
        var imageDirectoryPath = sharedLocations.GetImageDirectoryPath();

        await resourceResolver.LoadSiteImagesAsync(
            services, imageDirectoryPath, cancellationToken).ConfigureAwait(false);

        foreach (var eachSiteId in services.Select(x => x.Id))
        {
            if (!_imageTable.ContainsKey(eachSiteId))
            {
                var bitmapImage = new BitmapImage(new Uri(Path.Combine(imageDirectoryPath, $"{eachSiteId}.png")));

                // https://stackoverflow.com/questions/45893536/updating-image-source-from-a-separate-thread-in-wpf
                bitmapImage.Freeze();

                _imageTable.Add(eachSiteId, bitmapImage);
            }
        }
    }

    public CatalogDocument CatalogDocument
    {
        get
        {
            var doc = _catalogDocument;
            ArgumentNullException.ThrowIfNull(doc);
            return doc;
        }
    }

    public ImageSource? GetImage(string siteId)
        => _imageTable.TryGetValue(siteId, out ImageSource? value) ? value ?? null : null;
}

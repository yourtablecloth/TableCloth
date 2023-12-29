using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class ResourceCacheManager
{
    public ResourceCacheManager(
        CatalogDeserializer catalogDeserializer,
        SharedLocations sharedLocations,
        ResourceResolver resourceResolver)
    {
        _catalogDeserializer = catalogDeserializer;
        _sharedLocations = sharedLocations;
        _resourceResolver = resourceResolver;
    }

    private readonly CatalogDeserializer _catalogDeserializer;
    private readonly SharedLocations _sharedLocations;
    private readonly ResourceResolver _resourceResolver;

    private CatalogDocument? _catalogDocument;
    private Dictionary<string, ImageSource> _imageTable = new Dictionary<string, ImageSource>();

    public async Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
        => _catalogDocument = await _catalogDeserializer.DeserializeCatalogAsync(cancellationToken).ConfigureAwait(false);

    public async Task LoadSiteImages(CancellationToken cancellationToken = default)
    {
        var services = CatalogDocument.Services;
        var imageDirectoryPath = _sharedLocations.GetImageDirectoryPath();

        await _resourceResolver.LoadSiteImagesAsync(
            services, imageDirectoryPath, cancellationToken).ConfigureAwait(false);

        foreach (var eachSiteId in services.Select(x => x.Id))
        {
            if (!_imageTable.ContainsKey(eachSiteId))
                _imageTable.Add(eachSiteId, new BitmapImage(new Uri(Path.Combine(imageDirectoryPath, $"{eachSiteId}.png"))));
        }
    }

    public CatalogDocument CatalogDocument
        => _catalogDocument ?? throw new InvalidOperationException(StringResources.HostessError_CatalogLoadFailure(null));

    public ImageSource? GetImage(string siteId)
        => _imageTable.TryGetValue(siteId, out ImageSource? value) ? value ?? null : null;
}

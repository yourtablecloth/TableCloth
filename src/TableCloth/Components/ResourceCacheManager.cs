using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class ResourceCacheManager
{
    public ResourceCacheManager(
        SharedLocations sharedLocations,
        ResourceResolver resourceResolver)
    {
        _sharedLocations = sharedLocations;
        _resourceResolver = resourceResolver;
    }

    private readonly SharedLocations _sharedLocations;
    private readonly ResourceResolver _resourceResolver;

    private CatalogDocument? _catalogDocument;
    private Dictionary<string, ImageSource> _imageTable = new Dictionary<string, ImageSource>();

    public async Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
        => _catalogDocument = await _resourceResolver.DeserializeCatalogAsync(cancellationToken).ConfigureAwait(false);

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

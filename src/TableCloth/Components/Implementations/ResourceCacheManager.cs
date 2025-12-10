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

namespace TableCloth.Components.Implementations;

public sealed class ResourceCacheManager(
    ISharedLocations sharedLocations,
    IResourceResolver resourceResolver) : IResourceCacheManager
{
    private CatalogDocument? _catalogDocument;
    private readonly Dictionary<string, ImageSource> _imageTable = [];

    public async Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
    {
        var doc = await resourceResolver.DeserializeCatalogAsync(cancellationToken).ConfigureAwait(false);

        _catalogDocument = doc.Result;
        ArgumentNullException.ThrowIfNull(_catalogDocument);
        return _catalogDocument;
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

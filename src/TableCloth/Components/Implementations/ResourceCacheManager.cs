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

        if (doc.Result == null)
            throw new Exception("Cannot load catalog document from remote source.");

        return _catalogDocument = doc.Result;
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
        => _catalogDocument ?? throw new InvalidOperationException(StringResources.Error_With_Exception(ErrorStrings.Error_CatalogLoadFailure, null));

    public ImageSource? GetImage(string siteId)
        => _imageTable.TryGetValue(siteId, out ImageSource? value) ? value ?? null : null;
}

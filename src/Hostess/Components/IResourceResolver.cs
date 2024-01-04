using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Catalog;

namespace Hostess.Components
{
    public interface IResourceResolver
    {
        DateTimeOffset? CatalogLastModified { get; }

        Task<CatalogDocument> DeserializeCatalogAsync(CancellationToken cancellationToken = default);
    }
}
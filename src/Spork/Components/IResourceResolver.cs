using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace Spork.Components
{
    public interface IResourceResolver
    {
        DateTimeOffset? CatalogLastModified { get; }

        Task<ApiInvokeResult<CatalogDocument>> DeserializeCatalogAsync(CancellationToken cancellationToken = default);
    }
}
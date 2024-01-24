using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public interface IResourceResolver
{
    DateTimeOffset? CatalogLastModified { get; }

    Task<ApiInvokeResult<CatalogDocument?>> DeserializeCatalogAsync(CancellationToken cancellationToken = default);
    Task<ApiInvokeResult<Uri?>> GetReleaseDownloadUrlAsync(string owner, string repoName, CancellationToken cancellationToken = default);
    Task<ApiInvokeResult<string?>> GetLatestVersionAsync(string owner, string repoName, CancellationToken cancellationToken = default);
    Task<ApiInvokeResult<string?>> GetLicenseDescriptionForGitHubAsync(string owner, string repoName, CancellationToken cancellationToken = default);
    Task LoadSiteImagesAsync(IEnumerable<CatalogInternetService> services, string imageDirectoryPath, CancellationToken cancellationToken = default);
}
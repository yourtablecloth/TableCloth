using System;
using System.Threading.Tasks;
using TableCloth.Models;
using TableCloth.Resources;
using Windows.Services.Store;

namespace TableCloth.Components.Implementations;

public sealed class MicrosoftStoreAppUpdateManager : IAppUpdateManager
{
    public async Task<ApiInvokeResult<Uri?>> QueryNewVersionDownloadUrl()
    {
        var storeContext = StoreContext.GetDefault();
        var updates = await storeContext.GetAppAndOptionalStorePackageUpdatesAsync().AsTask().ConfigureAwait(false);

        if (updates.Count > 0)
        {
            // https://learn.microsoft.com/en-us/windows/uwp/launch-resume/launch-store-app
            var escapedProductId = Uri.EscapeDataString(ConstantStrings.MicrosoftStore_ProductId);
            var targetUrl = $"ms-windows-store://pdp/?ProductId={escapedProductId}";
            return new Uri(targetUrl);
        }
        else
            return default(Uri);
    }
}

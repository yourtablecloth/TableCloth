using System;
using System.Threading.Tasks;
using TableCloth.Models;

namespace TableCloth.Components
{
    public interface IAppUpdateManager
    {
        Task<ApiInvokeResult<Uri?>> QueryNewVersionDownloadUrl();
    }
}

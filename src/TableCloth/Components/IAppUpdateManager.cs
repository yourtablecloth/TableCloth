using System.Threading.Tasks;

namespace TableCloth.Components
{
    public interface IAppUpdateManager
    {
        Task<string?> QueryNewVersionDownloadUrl();
    }
}

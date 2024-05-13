using System.Threading;
using System.Threading.Tasks;

namespace Spork.Components
{
    public interface IShortcutCreator
    {
        Task<string> CreateShortcutOnDesktopAsync(string destinationPath, string linkName,
            string arguments = default, string iconFilePath = default, string description = default,
            CancellationToken cancellationToken = default);
    }
}

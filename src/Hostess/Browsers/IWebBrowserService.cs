using System.Diagnostics;
using System.Windows;

namespace Hostess.Browsers
{
    public interface IWebBrowserService
    {
        bool TryGetBrowserExecutablePath(out string executablePath);
    }
}

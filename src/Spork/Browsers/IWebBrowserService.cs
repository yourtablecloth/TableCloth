using System.Diagnostics;
using System.Windows;

namespace Spork.Browsers
{
    public interface IWebBrowserService
    {
        bool TryGetBrowserExecutablePath(out string executablePath);
    }
}

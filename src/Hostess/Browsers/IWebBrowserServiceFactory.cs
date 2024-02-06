namespace Hostess.Browsers
{
    public interface IWebBrowserServiceFactory
    {
        IWebBrowserService GetWebBrowserServiceByName(string name);

        IWebBrowserService GetWindowsSandboxDefaultBrowserService();
    }
}

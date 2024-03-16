namespace Spork.Browsers
{
    public interface IWebBrowserServiceFactory
    {
        IWebBrowserService GetWebBrowserServiceByName(string name);

        IWebBrowserService GetWindowsSandboxDefaultBrowserService();
    }
}

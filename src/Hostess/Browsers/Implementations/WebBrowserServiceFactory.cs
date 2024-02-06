using Microsoft.Extensions.DependencyInjection;
using System;

namespace Hostess.Browsers.Implementations
{
    public sealed class WebBrowserServiceFactory : IWebBrowserServiceFactory
    {
        public WebBrowserServiceFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private readonly IServiceProvider _serviceProvider;

        public IWebBrowserService GetWebBrowserServiceByName(string name)
            => _serviceProvider.GetRequiredKeyedService<IWebBrowserService>(name);

        public IWebBrowserService GetWindowsSandboxDefaultBrowserService()
            => GetWebBrowserServiceByName(nameof(X86ChromiumEdgeWebBrowserService));
    }
}

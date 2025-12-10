using Spork.Browsers;
using System.Diagnostics;
using TableCloth.Resources;

namespace Spork.Commands.AboutWindow
{
    public sealed class OpenSponsorPageCommand : CommandBase
    {
        public OpenSponsorPageCommand(
            IWebBrowserServiceFactory webBrowserServiceFactory)
        {
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _defaultWebBrowserService = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
        }

        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IWebBrowserService _defaultWebBrowserService;

        public override void Execute(object parameter)
            => Process.Start(_defaultWebBrowserService.CreateWebPageOpenRequest(CommonStrings.SponsorshipUrl));
    }
}

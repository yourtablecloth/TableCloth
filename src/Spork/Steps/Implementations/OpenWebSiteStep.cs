using Spork.Browsers;
using Spork.Components;
using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps.Implementations
{
    public sealed class OpenWebSiteStep : StepBase<OpenWebSiteItemViewModel>
    {
        public OpenWebSiteStep(
            IWebBrowserServiceFactory webBrowserServiceFactory)
        {
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _defaultWebBrowserService = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
        }

        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IWebBrowserService _defaultWebBrowserService;

        public override Task LoadContentForStepAsync(OpenWebSiteItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(OpenWebSiteItemViewModel viewModel, CancellationToken cancellationToken = default)
        {
            Process.Start(_defaultWebBrowserService.CreateWebPageOpenRequest(viewModel.TargetUrl, ProcessWindowStyle.Maximized));
            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => false;
    }
}

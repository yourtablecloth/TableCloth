using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Spork.Browsers;
using Spork.Components;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Events;
using TableCloth.Resources;

namespace Spork.ViewModels
{
    public partial class AboutWindowViewModelForDesigner : AboutWindowViewModel { }

    public partial class AboutWindowViewModel : ObservableObject
    {
        protected AboutWindowViewModel() { }

        [ActivatorUtilitiesConstructor]
        public AboutWindowViewModel(
            ILicenseDescriptor licenseDescriptor,
            IResourceResolver resourceResolver,
            IWebBrowserServiceFactory webBrowserServiceFactory,
            TaskFactory taskFactory)
        {
            _licenseDescriptor = licenseDescriptor;
            _resourceResolver = resourceResolver;
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _defaultWebBrowserService = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
            _taskFactory = taskFactory;
        }

        private readonly ILicenseDescriptor _licenseDescriptor;
        private readonly IResourceResolver _resourceResolver;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IWebBrowserService _defaultWebBrowserService;
        private readonly TaskFactory _taskFactory;

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        [RelayCommand]
        private void AboutWindowLoaded()
        {
            AppVersion = Helpers.GetAppVersion();
            CatalogVersion = _resourceResolver.CatalogLastModified?.ToString() ?? CommonStrings.UnknownText;
            LicenseDescription = _licenseDescriptor.GetLicenseDescriptions();
        }

        [RelayCommand]
        private Task AboutWindowClose()
        {
            return _taskFactory.StartNew(
                () => CloseRequested?.Invoke(this, new DialogRequestEventArgs(default)),
                default(CancellationToken));
        }

        [RelayCommand]
        private void OpenAppHomepage()
        {
            Process.Start(_defaultWebBrowserService.CreateWebPageOpenRequest(CommonStrings.AppInfoUrl));
        }

        [RelayCommand]
        private void OpenSponsorPage()
        {
            Process.Start(_defaultWebBrowserService.CreateWebPageOpenRequest(CommonStrings.SponsorshipUrl));
        }

        [ObservableProperty]
        private string _appVersion = CommonStrings.UnknownText;

        [ObservableProperty]
        private string _catalogVersion = CommonStrings.UnknownText;

        [ObservableProperty]
        private string _licenseDescription = default;

        [ObservableProperty]
        private string _appHomepageUrl = default;
    }
}

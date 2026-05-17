using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Spork.Browsers;
using Spork.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Events;
using TableCloth.Models.Sponsors;
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
            IHttpClientFactory httpClientFactory,
            TaskFactory taskFactory)
        {
            _licenseDescriptor = licenseDescriptor;
            _resourceResolver = resourceResolver;
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _httpClientFactory = httpClientFactory;
            _defaultWebBrowserService = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
            _taskFactory = taskFactory;
        }

        private readonly ILicenseDescriptor _licenseDescriptor;
        private readonly IResourceResolver _resourceResolver;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebBrowserService _defaultWebBrowserService;
        private readonly TaskFactory _taskFactory;

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        [RelayCommand]
        private async Task AboutWindowLoaded()
        {
            AppVersion = Helpers.GetAppVersion();
            CatalogVersion = _resourceResolver.CatalogLastModified?.ToString() ?? CommonStrings.UnknownText;
            LicenseDescription = _licenseDescriptor.GetLicenseDescriptions();
            await LoadSponsorsAsync();
        }

        private static readonly Random _random = new Random();

        private static List<T> Shuffle<T>(List<T> list)
        {
            var shuffled = new List<T>(list);
            int n = shuffled.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                T value = shuffled[k];
                shuffled[k] = shuffled[n];
                shuffled[n] = value;
            }
            return shuffled;
        }

        private async Task LoadSponsorsAsync()
        {
            try
            {
                using var httpClient = _httpClientFactory.CreateTableClothHttpClient();
                var response = await httpClient.GetAsync(CommonStrings.SponsorsJsonUrl);

                if (!response.IsSuccessStatusCode)
                {
                    HasSponsors = false;
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var sponsorsDocument = SponsorsDocument.Parse(json);

                if (sponsorsDocument?.Sponsors != null && sponsorsDocument.Sponsors.Count > 0)
                {
                    Sponsors = Shuffle(sponsorsDocument.Sponsors);
                    HasSponsors = true;
                }
                else
                {
                    HasSponsors = false;
                }
            }
            catch
            {
                HasSponsors = false;
            }
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

        [ObservableProperty]
        private List<SponsorInfo> _sponsors = new List<SponsorInfo>();

        [ObservableProperty]
        private bool _hasSponsors = false;
    }
}

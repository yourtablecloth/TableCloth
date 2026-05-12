using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Models.Sponsors;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class AboutWindowViewModelForDesigner : AboutWindowViewModel { }

public partial class AboutWindowViewModel : ObservableObject
{
    protected AboutWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
    public AboutWindowViewModel(
        IResourceResolver resourceResolver,
        ILicenseDescriptor licenseDescriptor,
        IAppMessageBox appMessageBox,
        IAppUpdateManager appUpdateManager,
        IHttpClientFactory httpClientFactory)
    {
        _resourceResolver = resourceResolver;
        _licenseDescriptor = licenseDescriptor;
        _appMessageBox = appMessageBox;
        _appUpdateManager = appUpdateManager;
        _httpClientFactory = httpClientFactory;
        _licenseDetails = UIStringResources.AboutWindow_LoadingLicensesMessage;
    }

    private readonly IResourceResolver _resourceResolver = default!;
    private readonly ILicenseDescriptor _licenseDescriptor = default!;
    private readonly IAppMessageBox _appMessageBox = default!;
    private readonly IAppUpdateManager _appUpdateManager = default!;
    private readonly IHttpClientFactory _httpClientFactory = default!;

    [RelayCommand]
    private async Task OnAboutWindowLoaded()
    {
        AppVersion = Helpers.GetAppVersion();
        CatalogDate = _resourceResolver.CatalogLastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? CommonStrings.UnknownText;
        LicenseDetails = await _licenseDescriptor.GetLicenseDescriptionsAsync();
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
    private void OpenWebsite()
    {
        Process.Start(new ProcessStartInfo(CommonStrings.AppInfoUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ShowSystemInfo()
    {
        var msinfoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "msinfo32.exe");

        if (!File.Exists(msinfoPath))
        {
            _appMessageBox.DisplayError(ErrorStrings.Error_Cannot_Run_SysInfo, false);
            return;
        }

        var psi = new ProcessStartInfo(msinfoPath);
        Process.Start(psi);
    }

    [RelayCommand]
    private async Task CheckUpdatedVersion()
    {
        try
        {
            // Velopack으로 설치된 경우 자동 업데이트
            if (_appUpdateManager.IsInstalledViaVelopack)
            {
                var hasUpdate = await _appUpdateManager.CheckForUpdatesAsync();

                if (hasUpdate)
                {
                    _appMessageBox.DisplayInfo(InfoStrings.Info_UpdateRequired);
                    await _appUpdateManager.DownloadAndApplyUpdatesAsync();
                    return;
                }

                _appMessageBox.DisplayInfo(InfoStrings.Info_UpdateNotRequired);
                return;
            }

            // Velopack으로 설치되지 않은 경우 GitHub Releases 페이지로 안내
            var releasesUrl = _appUpdateManager.GetReleasesPageUrl();
            var psi = new ProcessStartInfo(releasesUrl.AbsoluteUri) { UseShellExecute = true };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _appMessageBox.DisplayError(ex, false);
        }
    }

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        Process.Start(new ProcessStartInfo(CommonStrings.PrivacyPolicyUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenSponsorPage()
    {
        Process.Start(new ProcessStartInfo(CommonStrings.SponsorshipUrl) { UseShellExecute = true });
    }

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _catalogDate = string.Empty;

    [ObservableProperty]
    private string _licenseDetails = string.Empty;

    [ObservableProperty]
    private List<SponsorInfo> _sponsors = new();

    [ObservableProperty]
    private bool _hasSponsors = false;
}

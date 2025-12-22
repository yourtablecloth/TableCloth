using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using TableCloth.Commands.AboutWindow;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class AboutWindowViewModelForDesigner : AboutWindowViewModel { }

public partial class AboutWindowViewModel : ViewModelBase
{
    protected AboutWindowViewModel() { }

    public AboutWindowViewModel(
        AboutWindowLoadedCommand aboutWindowLoadedCommand,
        OpenWebsiteCommand openWebsiteCommand,
        ShowSystemInfoCommand showSystemInfoCommand,
        CheckUpdatedVersionCommand checkUpdatedVersionCommand,
        OpenPrivacyPolicyCommand openPrivacyPolicyCommand,
        OpenSponsorPageCommand openSponsorPageCommand)
    {
        _aboutWindowLoadedCommand = aboutWindowLoadedCommand;
        _openWebsiteCommand = openWebsiteCommand;
        _showSystemInfoCommand = showSystemInfoCommand;
        _checkUpdatedVersionCommand = checkUpdatedVersionCommand;
        _openPrivacyPolicyCommand = openPrivacyPolicyCommand;
        _openSponsorPageCommand = openSponsorPageCommand;

        _licenseDetails = UIStringResources.AboutWindow_LoadingLicensesMessage;
    }

    [RelayCommand]
    private void OnAboutWindowLoaded()
    {
        _aboutWindowLoadedCommand.Execute(this);
    }

    private AboutWindowLoadedCommand _aboutWindowLoadedCommand = default!;

    [RelayCommand]
    private void OpenWebsite()
    {
        _openWebsiteCommand.Execute(this);
    }

    private OpenWebsiteCommand _openWebsiteCommand = default!;

    [RelayCommand]
    private void ShowSystemInfo()
    {
        _showSystemInfoCommand.Execute(this);
    }

    private ShowSystemInfoCommand _showSystemInfoCommand = default!;

    [RelayCommand]
    private void CheckUpdatedVersion()
    {
        _checkUpdatedVersionCommand.Execute(this);
    }

    private CheckUpdatedVersionCommand _checkUpdatedVersionCommand = default!;

    [RelayCommand]
    private void OpenPrivacyPolicy()
    {
        _openPrivacyPolicyCommand.Execute(this);
    }

    private OpenPrivacyPolicyCommand _openPrivacyPolicyCommand = default!;

    [RelayCommand]
    private void OpenSponsorPage()
    {
        _openSponsorPageCommand.Execute(this);
    }

    private OpenSponsorPageCommand _openSponsorPageCommand = default!;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _catalogDate = string.Empty;

    [ObservableProperty]
    private string _licenseDetails = string.Empty;
}

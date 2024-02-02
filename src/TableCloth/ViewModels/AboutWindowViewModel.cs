using System;
using TableCloth.Commands.AboutWindow;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class AboutWindowViewModelForDesigner : AboutWindowViewModel { }

public class AboutWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected AboutWindowViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public AboutWindowViewModel(
        AboutWindowLoadedCommand aboutWindowLoadedCommand,
        OpenWebsiteCommand openWebsiteCommand,
        ShowSystemInfoCommand showSystemInfoCommand,
        CheckUpdatedVersionCommand checkUpdatedVersionCommand,
        OpenPrivacyPolicyCommand openPrivacyPolicyCommand)
    {
        _aboutWindowLoadedCommand = aboutWindowLoadedCommand;
        _openWebsiteCommand = openWebsiteCommand;
        _showSystemInfoCommand = showSystemInfoCommand;
        _checkUpdatedVersionCommand = checkUpdatedVersionCommand;
        _openPrivacyPolicyCommand = openPrivacyPolicyCommand;

        _licenseDetails = UIStringResources.AboutWindow_LoadingLicensesMessage;
    }

    private readonly AboutWindowLoadedCommand _aboutWindowLoadedCommand;
    private readonly OpenWebsiteCommand _openWebsiteCommand;
    private readonly ShowSystemInfoCommand _showSystemInfoCommand;
    private readonly CheckUpdatedVersionCommand _checkUpdatedVersionCommand;
    private readonly OpenPrivacyPolicyCommand _openPrivacyPolicyCommand;

    private string _appVersion = string.Empty;
    private string _catalogDate = string.Empty;
    private string _licenseDetails = string.Empty;

    public AboutWindowLoadedCommand AboutWindowLoadedCommand
        => _aboutWindowLoadedCommand;

    public OpenWebsiteCommand OpenWebsiteCommand
        => _openWebsiteCommand;

    public ShowSystemInfoCommand ShowSystemInfoCommand
        => _showSystemInfoCommand;

    public CheckUpdatedVersionCommand CheckUpdatedVersionCommand
        => _checkUpdatedVersionCommand;

    public OpenPrivacyPolicyCommand OpenPrivacyPolicyCommand
        => _openPrivacyPolicyCommand;

    public string AppVersion
    {
        get => _appVersion;
        set => SetProperty(ref _appVersion, value);
    }

    public string CatalogDate
    {
        get => _catalogDate;
        set => SetProperty(ref _catalogDate, value);
    }

    public string LicenseDetails
    {
        get => _licenseDetails;
        set => SetProperty(ref _licenseDetails, value);
    }
}

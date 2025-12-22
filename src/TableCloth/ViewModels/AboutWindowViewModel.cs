using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    private AboutWindowLoadedCommand _aboutWindowLoadedCommand = default!;

    [ObservableProperty] 
    private OpenWebsiteCommand _openWebsiteCommand = default!;

    [ObservableProperty]
    private ShowSystemInfoCommand _showSystemInfoCommand = default!;

    [ObservableProperty]
    private CheckUpdatedVersionCommand _checkUpdatedVersionCommand = default!;

    [ObservableProperty]
    private OpenPrivacyPolicyCommand _openPrivacyPolicyCommand = default!;

    [ObservableProperty]
    private OpenSponsorPageCommand _openSponsorPageCommand = default!;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private string _catalogDate = string.Empty;

    [ObservableProperty]
    private string _licenseDetails = string.Empty;
}

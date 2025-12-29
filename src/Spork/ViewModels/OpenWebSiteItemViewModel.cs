using CommunityToolkit.Mvvm.ComponentModel;

namespace Spork.ViewModels
{
    public partial class OpenWebSiteItemViewModel : InstallItemViewModel
    {
        [ObservableProperty]
        private string _targetUrl;
    }
}

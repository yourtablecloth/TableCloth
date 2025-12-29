using CommunityToolkit.Mvvm.ComponentModel;

namespace Spork.ViewModels
{
    public partial class EdgeExtensionInstallItemViewModel : InstallItemViewModel
    {
        [ObservableProperty]
        private string _edgeCrxUrl;

        [ObservableProperty]
        private string _edgeExtensionId;
    }
}

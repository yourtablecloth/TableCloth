using CommunityToolkit.Mvvm.ComponentModel;

namespace Spork.ViewModels
{
    public partial class PackageInstallItemViewModel : InstallItemViewModel
    {
        [ObservableProperty]
        private string _downloadedFilePath;

        [ObservableProperty]
        private string _packageUrl;

        [ObservableProperty]
        private string _arguments;
    }
}

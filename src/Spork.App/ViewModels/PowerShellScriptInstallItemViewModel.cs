using CommunityToolkit.Mvvm.ComponentModel;

namespace Spork.ViewModels
{
    public sealed partial class PowerShellScriptInstallItemViewModel : InstallItemViewModel
    {
        [ObservableProperty]
        private string _downloadedScriptFilePath;

        [ObservableProperty]
        private string _scriptContent;
    }
}

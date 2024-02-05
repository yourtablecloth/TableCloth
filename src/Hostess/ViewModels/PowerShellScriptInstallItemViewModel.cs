namespace Hostess.ViewModels
{
    public sealed class PowerShellScriptInstallItemViewModel : InstallItemViewModel
    {
        private string _downloadedScriptFilePath;
        private string _scriptContent;

        public string DownloadedScriptFilePath
        {
            get => _downloadedScriptFilePath;
            set => SetProperty(ref _downloadedScriptFilePath, value);
        }

        public string ScriptContent
        {
            get => _scriptContent;
            set => SetProperty(ref _scriptContent, value);
        }
    }
}

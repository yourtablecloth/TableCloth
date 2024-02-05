using System;

namespace Hostess.ViewModels
{
    [Serializable]
    public class PackageInstallItemViewModel : InstallItemViewModel
    {
        private string _downloadedFilePath;
        private string _packageUrl;
        private string _arguments;

        public string DownloadedFilePath
        {
            get => _downloadedFilePath;
            set => SetProperty(ref _downloadedFilePath, value);
        }

        public string PackageUrl
        {
            get => _packageUrl;
            set => SetProperty(ref _packageUrl, value);
        }

        public string Arguments
        {
            get => _arguments;
            set => SetProperty(ref _arguments, value);
        }
    }
}

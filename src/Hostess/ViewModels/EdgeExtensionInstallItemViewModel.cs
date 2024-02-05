using System;

namespace Hostess.ViewModels
{
    [Serializable]
    public class EdgeExtensionInstallItemViewModel : InstallItemViewModel
    {
        private string _edgeCrxUrl;
        private string _edgeExtensionId;

        public string EdgeCrxUrl
        {
            get => _edgeCrxUrl;
            set => SetProperty(ref _edgeCrxUrl, value);
        }

        public string EdgeExtensionId
        {
            get => _edgeExtensionId;
            set => SetProperty(ref _edgeExtensionId, value);
        }
    }
}

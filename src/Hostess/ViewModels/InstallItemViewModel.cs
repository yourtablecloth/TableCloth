using System;
using TableCloth.ViewModels;

namespace Hostess.ViewModels
{
    [Serializable]
    public sealed class InstallItemViewModel : ViewModelBase
    {
        private InstallItemType _installItemType;
        private string _targetSiteName;
        private string _targetSiteUrl;
        private string _packageName;
        private string _packageUrl;
        private string _arguments;
        private string _scriptContent;
        private bool? _installed;
        private string _statusMessage;
        private string _errorMessage;

        public InstallItemType InstallItemType
        {
            get => _installItemType;
            set => SetProperty(ref _installItemType, value);
        }

        public string TargetSiteName
        {
            get => _targetSiteName;
            set => SetProperty(ref _targetSiteName, value);
        }

        public string TargetSiteUrl
        {
            get => _targetSiteUrl;
            set => SetProperty(ref _targetSiteUrl, value);
        }

        public string PackageName
        {
            get => _packageName;
            set => SetProperty(ref _packageName, value);
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

        public string ScriptContent
        {
            get => _scriptContent;
            set => SetProperty(ref _scriptContent, value);
        }

        public bool? Installed
        {
            get => _installed;
            set => SetProperty(ref _installed, value, new string[] { nameof(Installed), nameof(StatusMessage), nameof(ErrorMessage), nameof(InstallFlags), nameof(ShowErrorMessageLink), });
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value, new string[] { nameof(StatusMessage), nameof(Installed), nameof(ErrorMessage), nameof(InstallFlags), nameof(ShowErrorMessageLink), });
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value, new string[] { nameof(ErrorMessage), nameof(StatusMessage), nameof(Installed), nameof(InstallFlags), nameof(ShowErrorMessageLink), });
        }

        public bool ShowErrorMessageLink
            => !string.IsNullOrWhiteSpace(_errorMessage) && _installed.HasValue && !_installed.Value;

        public string InstallFlags
            => $"{(_installed.HasValue ? _installed.Value ? "\u2714\uFE0F" : "\u274C\uFE0F" : "\u23F3\uFE0F")}";

        public override string ToString()
            => $"{InstallFlags} {TargetSiteName} {PackageName} {StatusMessage}";
    }
}

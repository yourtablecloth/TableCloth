using System;
using System.ComponentModel;
using System.Windows.Input;

namespace Hostess
{
    [Serializable]
    public sealed class InstallItemViewModel : INotifyPropertyChanged
    {
        private string _targetSiteName;
        private string _packageName;
        private string _packageUrl;
        private string _arguments;
        private bool? _installed;
        private string _statusMessage;
        private string _errorMessage;

        private readonly ICommand _showErrorMessageCommand = new ShowErrorMessageCommand();

        public event PropertyChangedEventHandler PropertyChanged;

        public string TargetSiteName
        {
            get => _targetSiteName;
            set
            {
                if (string.Equals(_targetSiteName, value, StringComparison.Ordinal))
                {
                    return;
                }

                _targetSiteName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TargetSiteName)));
            }
        }

        public string PackageName
        {
            get => _packageName;
            set
            {
                if (string.Equals(_packageName, value, StringComparison.Ordinal))
                {
                    return;
                }

                _packageName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageName)));
            }
        }

        public string PackageUrl
        {
            get => _packageUrl;
            set
            {
                if (string.Equals(_packageUrl, value, StringComparison.Ordinal))
                {
                    return;
                }

                _packageUrl = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageUrl)));
            }
        }

        public string Arguments
        {
            get => _arguments;
            set
            {
                if (string.Equals(_arguments, value, StringComparison.Ordinal))
                {
                    return;
                }

                _arguments = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Arguments)));
            }
        }

        public bool? Installed
        {
            get => _installed;
            set
            {
                if (_installed == value)
                {
                    return;
                }

                _installed = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Installed)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstallFlags)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowErrorMessageLink)));
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value)
                {
                    return;
                }

                _statusMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Installed)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstallFlags)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowErrorMessageLink)));
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage == value)
                {
                    return;
                }

                _errorMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Installed)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ErrorMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstallFlags)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowErrorMessageLink)));
            }
        }

        public ICommand ShowErrorMessage
            => _showErrorMessageCommand;

        public bool ShowErrorMessageLink
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_errorMessage))
                    return false;

                if (!_installed.HasValue)
                    return false;

                if (_installed.Value)
                    return false;

                return true;
            }
        }

        public string InstallFlags
            => $"{(_installed.HasValue ? _installed.Value ? "\u2714\uFE0F" : "\u274C\uFE0F" : "\u23F3\uFE0F")}";

        public override string ToString()
            => $"{InstallFlags} {TargetSiteName} {PackageName} {StatusMessage}";
    }
}

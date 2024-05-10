using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sponge
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel()
        {
        }

        private int _progressRate = 0;
        private string _progressMessage = string.Empty;
        private bool _workInProgress = false;
        private bool _overwriteMultipleTimes = false;

        public int ProgressRate
        {
            get => _progressRate;
            set
            {
                if (_progressRate == value) return;
                _progressRate = value;
                OnPropertyChanged();
            }
        }

        public string ProgressMessage
        {
            get => _progressMessage;
            set
            {
                if (_progressMessage == value) return;
                _progressMessage = value;
                OnPropertyChanged();
            }
        }

        public bool WorkInProgress
        {
            get => _workInProgress;
            set
            {
                if (_workInProgress == value) return;
                _workInProgress = value;
                OnPropertyChanged();
            }
        }

        public bool OverwriteMultipleTimes
        {
            get => _overwriteMultipleTimes;
            set
            {
                if (_overwriteMultipleTimes == value) return;
                _overwriteMultipleTimes = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

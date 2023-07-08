using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Components;

namespace TableCloth.ViewModels
{
    public class InputPasswordWindowViewModel : INotifyPropertyChanged
    {
        public InputPasswordWindowViewModel(
            X509CertPairScanner certPairScanner,
            AppMessageBox appMessageBox)
        {
            _certPairScanner = certPairScanner;
            _appMessageBox = appMessageBox;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly X509CertPairScanner _certPairScanner;
        private readonly AppMessageBox _appMessageBox;

        public event PropertyChangedEventHandler PropertyChanged;

        public X509CertPairScanner CertPairScanner
            => _certPairScanner;

        public AppMessageBox AppMessageBox
            => _appMessageBox;
    }
}

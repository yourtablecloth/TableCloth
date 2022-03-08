using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Contracts;

namespace TableCloth.ViewModels
{
    public class InputPasswordWindowViewModel : INotifyPropertyChanged
    {
        public InputPasswordWindowViewModel(
            IX509CertPairScanner certPairScanner,
            IAppMessageBox appMessageBox)
        {
            _certPairScanner = certPairScanner;
            _appMessageBox = appMessageBox;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly IX509CertPairScanner _certPairScanner;
        private readonly IAppMessageBox _appMessageBox;

        public event PropertyChangedEventHandler PropertyChanged;

        public IX509CertPairScanner CertPairScanner
            => _certPairScanner;

        public IAppMessageBox AppMessageBox
            => _appMessageBox;
    }
}

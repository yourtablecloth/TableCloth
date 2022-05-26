using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Contracts;

namespace TableCloth.ViewModels
{
    public class AboutWindowViewModel : INotifyPropertyChanged
    {
        public AboutWindowViewModel(
            IAppMessageBox appMessageBox,
            ICatalogDeserializer catalogDeserializer)
        {
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly IAppMessageBox _appMessageBox;
        private readonly ICatalogDeserializer _catalogDeserializer;

        public event PropertyChangedEventHandler PropertyChanged;

        public DateTimeOffset? CatalogVersion
            => _catalogDeserializer.CatalogLastModified;

        public IAppMessageBox AppMessageBox
            => _appMessageBox;
    }
}

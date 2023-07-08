using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Components;

namespace TableCloth.Models.ViewModels
{
    public class AboutWindowViewModel : INotifyPropertyChanged
    {
        public AboutWindowViewModel(
            AppMessageBox appMessageBox,
            CatalogDeserializer catalogDeserializer)
        {
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly AppMessageBox _appMessageBox;
        private readonly CatalogDeserializer _catalogDeserializer;

        public event PropertyChangedEventHandler PropertyChanged;

        public DateTimeOffset? CatalogVersion
            => _catalogDeserializer.CatalogLastModified;

        public AppMessageBox AppMessageBox
            => _appMessageBox;
    }
}

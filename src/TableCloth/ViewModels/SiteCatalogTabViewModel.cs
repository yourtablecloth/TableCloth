using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Models.Catalog;

namespace TableCloth.ViewModels
{
    public class SiteCatalogTabViewModel : INotifyPropertyChanged
    {
        public SiteCatalogTabViewModel()
        {
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private CatalogInternetServiceCategory _category;
        private List<CatalogInternetService> _sites;

        public event PropertyChangedEventHandler PropertyChanged;

        public CatalogInternetServiceCategory Category
        {
            get => _category;
            set
            {
                if (value != _category)
                {
                    _category = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<CatalogInternetService> Sites
        {
            get => _sites;
            set
            {
                if (value != _sites)
                {
                    _sites = value;
                    NotifyPropertyChanged();
                }
            }
        }        
    }
}

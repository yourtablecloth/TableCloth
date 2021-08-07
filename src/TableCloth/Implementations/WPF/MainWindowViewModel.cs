using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Implementations.WPF
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel(
            IAppMessageBox appMessageBox,
            ICatalogDeserializer catalogDeserializer)
        {
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;

            CatalogDocument = _catalogDeserializer.DeserializeCatalog(
                new Uri(StringResources.CatalogUrl, UriKind.Absolute));

            Catalogs = CatalogDocument.Services
                .GroupBy(x => x.Category)
                .Select(x => new SiteCatalogTabViewModel
                {
                    Category = x.Key,
                    Sites = x.ToList(),
                })
                .ToList();
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly IAppMessageBox _appMessageBox;
        private readonly ICatalogDeserializer _catalogDeserializer;

        private CatalogDocument _catalogDocument;
        private List<string> _selectedCertFiles;
        private List<SiteCatalogTabViewModel> _catalogs;
        private SiteCatalogTabViewModel _selectedTabView;

        public event PropertyChangedEventHandler PropertyChanged;

        public IAppMessageBox AppMessageBox
            => _appMessageBox;

        public CatalogDocument CatalogDocument
        {
            get => _catalogDocument;
            set
            {
                if (value != _catalogDocument)
                {
                    _catalogDocument = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<string> SelectedCertFiles
        {
            get => _selectedCertFiles;
            set
            {
                if (value != _selectedCertFiles)
                {
                    _selectedCertFiles = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<SiteCatalogTabViewModel> Catalogs
        {
            get => _catalogs;
            set
            {
                if (value != _catalogs)
                {
                    _catalogs = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public SiteCatalogTabViewModel SelectedTabView
        {
            get => _selectedTabView;
            set
            {
                if (value != _selectedTabView)
                {
                    _selectedTabView = value;
                    NotifyPropertyChanged();
                }
            }
        }
    }
}

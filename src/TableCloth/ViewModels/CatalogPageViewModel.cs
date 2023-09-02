using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels
{
    public class CatalogPageViewModel : INotifyPropertyChanged
    {
        public CatalogPageViewModel(
            CatalogCacheManager catalogCacheManager,
            AppRestartManager appRestartManager)
        {
            _catalogCacheManager = catalogCacheManager;
            _appRestartManager = appRestartManager;

            CatalogDocument = _catalogCacheManager.CatalogDocument;
            Services = CatalogDocument.Services.ToList();
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly CatalogCacheManager _catalogCacheManager;
        private readonly AppRestartManager _appRestartManager;

        private CatalogDocument _catalogDocument;
        private List<CatalogInternetService> _services;

        public event PropertyChangedEventHandler PropertyChanged;

        public CatalogCacheManager CatalogCacheManager
            => _catalogCacheManager;

        public AppRestartManager AppRestartManager
            => _appRestartManager;

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

        public List<CatalogInternetService> Services
        {
            get => _services;
            set
            {
                if (value != _services)
                {
                    _services = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool HasServices
            => Services != null && Services.Any();
    }
}

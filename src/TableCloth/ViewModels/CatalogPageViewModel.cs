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
            SharedLocations sharedLocations,
            AppStartup appStartup,
            AppMessageBox appMessageBox,
            CatalogDeserializer catalogDeserializer,
            AppRestartManager appRestartManager)
        {
            _sharedLocations = sharedLocations;
            _appStartup = appStartup;
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;
            _appRestartManager = appRestartManager;

            try
            {
                CatalogDocument = _catalogDeserializer.DeserializeCatalog();
                Services = CatalogDocument.Services.ToList();
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, false);
                CatalogDocument = new CatalogDocument();
                Services = Array.Empty<CatalogInternetService>().ToList();
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly SharedLocations _sharedLocations;
        private readonly AppStartup _appStartup;
        private readonly AppMessageBox _appMessageBox;
        private readonly CatalogDeserializer _catalogDeserializer;
        private readonly AppRestartManager _appRestartManager;

        private CatalogDocument _catalogDocument;
        private List<CatalogInternetService> _services;

        public event PropertyChangedEventHandler PropertyChanged;

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

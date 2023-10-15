using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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
            CatalogCacheManager catalogCacheManager,
            ResourceResolver resourceResolver,
            AppRestartManager appRestartManager)
        {
            _sharedLocations = sharedLocations;
            _catalogCacheManager = catalogCacheManager;
            _resourceResolver = resourceResolver;
            _appRestartManager = appRestartManager;

            CatalogDocument = _catalogCacheManager.CatalogDocument;
            Services = CatalogDocument.Services.OrderBy(service => service.Category.GetType().GetField(service.Category.ToString())
                ?.GetCustomAttribute<EnumDisplayOrderAttribute>()
                ?.Order ?? 0).ToList();
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly SharedLocations _sharedLocations;
        private readonly CatalogCacheManager _catalogCacheManager;
        private readonly ResourceResolver _resourceResolver;
        private readonly AppRestartManager _appRestartManager;

        private CatalogDocument _catalogDocument;
        private List<CatalogInternetService> _services;
        private CatalogInternetService _selectedService;
        private CatalogInternetServiceCategory? _selectedServiceCategory;

        public event PropertyChangedEventHandler PropertyChanged;

        public SharedLocations SharedLocations
            => _sharedLocations;

        public CatalogCacheManager CatalogCacheManager
            => _catalogCacheManager;

        public ResourceResolver ResourceResolver
            => _resourceResolver;

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

        public CatalogInternetService SelectedService
        {
            get => _selectedService;
            set
            {
                if (value != _selectedService)
                {
                    _selectedService = value;
                    NotifyPropertyChanged();

                    if (value != null)
                        SelectedServiceCategory = value.Category;
                }
            }
        }

        public CatalogInternetServiceCategory? SelectedServiceCategory
        {
            get => _selectedServiceCategory;
            set
            {
                if (value != _selectedServiceCategory)
                {
                    _selectedServiceCategory = value;
                    NotifyPropertyChanged();
                }
            }
        }
    }
}

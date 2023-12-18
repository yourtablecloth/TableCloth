using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TableCloth.Commands;
using TableCloth.Components;
using TableCloth.Models.Catalog;

namespace TableCloth.ViewModels
{
    public class CatalogPageViewModel : ViewModelBase
    {
        public CatalogPageViewModel(
            CatalogCacheManager catalogCacheManager,
            AppRestartCommand appRestartCommand,
            AboutThisAppCommand aboutThisAppCommand)
        {
            _appRestartCommand = appRestartCommand;
            _aboutThisAppCommand = aboutThisAppCommand;

            CatalogDocument = catalogCacheManager.CatalogDocument;
            Services = CatalogDocument.Services.OrderBy(service => service.Category.GetType().GetField(service.Category.ToString())
                ?.GetCustomAttribute<EnumDisplayOrderAttribute>()
                ?.Order ?? 0).ToList();
        }

        private readonly AppRestartCommand _appRestartCommand;
        private readonly AboutThisAppCommand _aboutThisAppCommand;

        private CatalogDocument _catalogDocument;
        private List<CatalogInternetService> _services;
        private CatalogInternetService _selectedService;
        private CatalogInternetServiceCategory? _selectedServiceCategory;

        public AppRestartCommand AppRestartCommand
            => _appRestartCommand;

        public AboutThisAppCommand AboutThisAppCommand
            => _aboutThisAppCommand;

        public CatalogDocument CatalogDocument
        {
            get => _catalogDocument;
            set => SetProperty(ref _catalogDocument, value);
        }

        public List<CatalogInternetService> Services
        {
            get => _services;
            set => SetProperty(ref _services, value);
        }

        public bool HasServices
            => Services != null && Services.Any();

        public CatalogInternetService SelectedService
        {
            get => _selectedService;
            set
            {
                if (SetProperty(ref _selectedService, value) && value != null)
                    SelectedServiceCategory = value.Category;
            }
        }

        public CatalogInternetServiceCategory? SelectedServiceCategory
        {
            get => _selectedServiceCategory;
            set => SetProperty(ref _selectedServiceCategory, value);
        }
    }
}

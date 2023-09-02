using System.Collections.Generic;
using TableCloth.Models.Catalog;

namespace TableCloth.Models
{
    public sealed class DetailPageModel
    {
        public DetailPageModel(
            CatalogInternetService selectedService)
        {
            SelectedService = selectedService;
        }

        public CatalogInternetService SelectedService { get; private set; } = default;
    }
}

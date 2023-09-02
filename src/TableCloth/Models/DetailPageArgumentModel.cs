using System.Collections.Generic;
using TableCloth.Models.Catalog;

namespace TableCloth.Models
{
    public sealed class DetailPageArgumentModel
    {
        public DetailPageArgumentModel(
            CatalogInternetService selectedService)
        {
            SelectedService = selectedService;
        }

        public CatalogInternetService SelectedService { get; private set; } = default;
    }
}

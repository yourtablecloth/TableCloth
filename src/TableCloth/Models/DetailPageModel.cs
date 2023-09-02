using System.Collections.Generic;
using TableCloth.Models.Catalog;

namespace TableCloth.Models
{
    public sealed class DetailPageModel
    {
        public DetailPageModel(
            IEnumerable<CatalogInternetService> selectedServices)
        {
            SelectedServices = selectedServices;
        }

        public IEnumerable<CatalogInternetService> SelectedServices { get; private set; } = default;
    }
}

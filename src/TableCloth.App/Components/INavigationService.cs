using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public interface INavigationService
{
    bool NavigateToCatalog(string searchKeyword);
    bool NavigateToDetail(string searchKeyword, CatalogInternetService selectedService, CommandLineArgumentModel? commandLineArgumentModel);
    bool NavigateToQuickStart();
    void GoBack();
}
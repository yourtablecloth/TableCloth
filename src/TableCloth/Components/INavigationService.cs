using System.Windows.Controls;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public interface INavigationService
{
    Frame FindNavigationFrameFromMainWindow();
    string GetPageFrameControlName();
    bool NavigateToCatalog(string searchKeyword);
    bool NavigateToDetail(string searchKeyword, CatalogInternetService selectedService, CommandLineArgumentModel? commandLineArgumentModel);
    void GoBack();
}
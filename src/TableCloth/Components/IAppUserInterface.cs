using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface IAppUserInterface
{
    AboutWindow CreateAboutWindow();
    CatalogPage CreateCatalogPage(string searchKeyword);
    CatalogPageViewModel CreateCatalogPageViewModel(string searchKeyword);
    CertSelectWindow CreateCertSelectWindow();
    DetailPage CreateDetailPage(CatalogInternetService selectedService, CommandLineArgumentModel? commandLineArgumentModel);
    DetailPageViewModel CreateDetailPageViewModel(CatalogInternetService selectedService, CommandLineArgumentModel? commandLineArgumentModel);
    DisclaimerWindow CreateDisclaimerWindow();
    InputPasswordWindow CreateInputPasswordWindow();
    SplashScreen CreateSplashScreen();
}
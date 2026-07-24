using Avalonia.Controls;
using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface IAppUserInterface
{
    // 이슈 #296: WPF window.ShowDialog()(동기) 대체. 소유자를 내부 해석하고 동기 모달로 표시 후 결과 반환.
    bool? ShowDialog(Window window);

    AboutWindow CreateAboutWindow();
    OptionsWindow CreateOptionsWindow(string? initialTabKey = null);
    PowerSchemeGuideWindow CreatePowerSchemeGuideWindow();
    CatalogPage CreateCatalogPage(string searchKeyword);
    CatalogPageViewModel CreateCatalogPageViewModel(string searchKeyword);
    QuickStartPage CreateQuickStartPage();
    QuickStartPageViewModel CreateQuickStartPageViewModel();
    CertSelectWindow CreateCertSelectWindow(X509CertPair? previousCertPair);
    DetailPage CreateDetailPage(string searchKeyword, CatalogInternetService selectedService, CommandLineArgumentModel? commandLineArgumentModel);
    DetailPageViewModel CreateDetailPageViewModel(CatalogInternetService selectedService, CommandLineArgumentModel? commandLineArgumentModel);
    DisclaimerWindow CreateDisclaimerWindow();
    InputPasswordWindow CreateInputPasswordWindow();
    SplashScreen CreateSplashScreen();
}
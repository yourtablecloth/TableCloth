using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Components.Implementations;

// 이슈 #296: Avalonia 는 Window.Owner 세터가 없어(소유는 ShowDialog(owner) 시점 성립), WPF 시절 SetOwnerIfAvailable
// 사전 설정을 제거하고 ShowDialog 시 소유자를 해석한다.
public sealed class AppUserInterface(
    IServiceProvider serviceProvider,
    IResourceCacheManager resourceCacheManager,
    IApplicationService applicationService) : IAppUserInterface
{
    public AboutWindow CreateAboutWindow()
        => serviceProvider.GetRequiredService<AboutWindow>();

    public OptionsWindow CreateOptionsWindow(string? initialTabKey = null)
    {
        var window = serviceProvider.GetRequiredService<OptionsWindow>();
        window.ViewModel.SetInitialTab(initialTabKey);
        return window;
    }

    public CertSelectWindow CreateCertSelectWindow(X509CertPair? previousCertPair)
    {
        var window = serviceProvider.GetRequiredService<CertSelectWindow>();

        if (previousCertPair != null)
            window.ViewModel.PreviousCertPairHash = previousCertPair?.CertHash;

        return window;
    }

    public InputPasswordWindow CreateInputPasswordWindow()
        => serviceProvider.GetRequiredService<InputPasswordWindow>();

    public DisclaimerWindow CreateDisclaimerWindow()
        => serviceProvider.GetRequiredService<DisclaimerWindow>();

    public PowerSchemeGuideWindow CreatePowerSchemeGuideWindow()
        => serviceProvider.GetRequiredService<PowerSchemeGuideWindow>();

    public SplashScreen CreateSplashScreen()
        => serviceProvider.GetRequiredService<SplashScreen>();

    public CatalogPage CreateCatalogPage(string searchKeyword)
    {
        var catalogPage = serviceProvider.GetRequiredService<CatalogPage>();
        catalogPage.ViewModel.SearchKeyword = searchKeyword;
        return catalogPage;
    }

    public CatalogPageViewModel CreateCatalogPageViewModel(string searchKeyword)
    {
        var viewModel = serviceProvider.GetRequiredService<CatalogPageViewModel>();
        viewModel.SearchKeyword = searchKeyword;
        return viewModel;
    }

    public QuickStartPage CreateQuickStartPage()
        => serviceProvider.GetRequiredService<QuickStartPage>();

    public QuickStartPageViewModel CreateQuickStartPageViewModel()
        => serviceProvider.GetRequiredService<QuickStartPageViewModel>();

    public DetailPage CreateDetailPage(
        string searchKeyword,
        CatalogInternetService selectedService,
        CommandLineArgumentModel? commandLineArgumentModel)
    {
        var detailPage = new DetailPage(CreateDetailPageViewModel(selectedService, commandLineArgumentModel));
        detailPage.ViewModel.SearchKeyword = searchKeyword;
        return detailPage;
    }

    public DetailPageViewModel CreateDetailPageViewModel(
        CatalogInternetService selectedService,
        CommandLineArgumentModel? commandLineArgumentModel)
    {
        var viewModel = serviceProvider.GetRequiredService<DetailPageViewModel>();
        viewModel.SelectedService = selectedService;
        viewModel.CommandLineArgumentModel = commandLineArgumentModel;

        if (viewModel.CommandLineArgumentModel != null)
        {
            var commandLineSelectedService = resourceCacheManager.CatalogDocument?.Services
                .Where(x => viewModel.CommandLineArgumentModel.SelectedServices.Contains(x.Id))
                .FirstOrDefault();

            viewModel.SelectedService = commandLineSelectedService;
        }

        return viewModel;
    }

    // 이슈 #296: WPF window.ShowDialog()(동기) 대체. 소유자를 내부에서 해석하고 동기 모달로 표시 후 결과 반환.
    public bool? ShowDialog(Window window)
    {
        var result = applicationService.DispatchInvoke(new Func<bool?>(() =>
        {
            var owner = applicationService.GetActiveWindow() ?? applicationService.GetMainWindow();
            return DialogHost.ShowModal(window, owner);
        }), []);

        return result as bool?;
    }
}

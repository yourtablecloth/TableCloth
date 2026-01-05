using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Windows;
using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Components.Implementations;

public sealed class AppUserInterface(
    IServiceProvider serviceProvider,
    IResourceCacheManager resourceCacheManager,
    IApplicationService applicationService) : IAppUserInterface
{
    private TWindow SetOwnerIfAvailable<TWindow>(TWindow window)
        where TWindow : Window
    {
        var owner = applicationService.GetActiveWindow() ?? applicationService.GetMainWindow();

        if (owner != null && !ReferenceEquals(owner, window))
            window.Owner = owner;

        return window;
    }

    public AboutWindow CreateAboutWindow()
        => SetOwnerIfAvailable(serviceProvider.GetRequiredService<AboutWindow>());

    public CertSelectWindow CreateCertSelectWindow(X509CertPair? previousCertPair)
    {
        var window = SetOwnerIfAvailable(serviceProvider.GetRequiredService<CertSelectWindow>());

        if (previousCertPair != null)
            window.ViewModel.PreviousCertPairHash = previousCertPair?.CertHash;

        return window;
    }

    public InputPasswordWindow CreateInputPasswordWindow()
        => SetOwnerIfAvailable(serviceProvider.GetRequiredService<InputPasswordWindow>());

    public DisclaimerWindow CreateDisclaimerWindow()
        => SetOwnerIfAvailable(serviceProvider.GetRequiredService<DisclaimerWindow>());

    public SplashScreen CreateSplashScreen()
        => SetOwnerIfAvailable(serviceProvider.GetRequiredService<SplashScreen>());

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
}

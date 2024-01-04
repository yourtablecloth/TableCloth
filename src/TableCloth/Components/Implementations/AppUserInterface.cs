using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public sealed class AppUserInterface(
    IServiceProvider serviceProvider,
    IResourceCacheManager resourceCacheManager) : IAppUserInterface
{
    public AboutWindow CreateAboutWindow()
        => serviceProvider.GetRequiredService<AboutWindow>();

    public CertSelectWindow CreateCertSelectWindow()
        => serviceProvider.GetRequiredService<CertSelectWindow>();

    public InputPasswordWindow CreateInputPasswordWindow()
        => serviceProvider.GetRequiredService<InputPasswordWindow>();

    public DisclaimerWindow CreateDisclaimerWindow()
        => serviceProvider.GetRequiredService<DisclaimerWindow>();

    public SplashScreen CreateSplashScreen()
        => serviceProvider.GetRequiredService<SplashScreen>();

    public CatalogPage CreateCatalogPage(string searchKeyword)
        => new CatalogPage(CreateCatalogPageViewModel(searchKeyword));

    public CatalogPageViewModel CreateCatalogPageViewModel(string searchKeyword)
    {
        var viewModel = serviceProvider.GetRequiredService<CatalogPageViewModel>();
        viewModel.SearchKeyword = searchKeyword;
        return viewModel;
    }

    public DetailPage CreateDetailPage(
        CatalogInternetService selectedService,
        CommandLineArgumentModel? commandLineArgumentModel)
        => new DetailPage(CreateDetailPageViewModel(selectedService, commandLineArgumentModel));

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

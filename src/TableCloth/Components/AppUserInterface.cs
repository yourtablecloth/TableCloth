using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public sealed class AppUserInterface
{
    public AppUserInterface(
        IServiceProvider serviceProvider,
        CatalogCacheManager catalogCacheManager)
    {
        _serviceProvider = serviceProvider;
        _catalogCacheManager = catalogCacheManager;
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly CatalogCacheManager _catalogCacheManager;

    public AboutWindow CreateAboutWindow()
        => _serviceProvider.GetRequiredService<AboutWindow>();

    public CertSelectWindow CreateCertSelectWindow()
        => _serviceProvider.GetRequiredService<CertSelectWindow>();

    public InputPasswordWindow CreateInputPasswordWindow()
        => _serviceProvider.GetRequiredService<InputPasswordWindow>();

    public DisclaimerWindow CreateDisclaimerWindow()
        => _serviceProvider.GetRequiredService<DisclaimerWindow>();

    public SplashScreen CreateSplashScreen()
        => _serviceProvider.GetRequiredService<SplashScreen>();

    public CatalogPage CreateCatalogPage(string searchKeyword)
        => new CatalogPage(CreateCatalogPageViewModel(searchKeyword));

    public CatalogPageViewModel CreateCatalogPageViewModel(string searchKeyword)
    {
        var viewModel = _serviceProvider.GetRequiredService<CatalogPageViewModel>();
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
        var viewModel = _serviceProvider.GetRequiredService<DetailPageViewModel>();
        viewModel.SelectedService = selectedService;
        viewModel.CommandLineArgumentModel = commandLineArgumentModel;

        if (viewModel.CommandLineArgumentModel != null)
        {
            var commandLineSelectedService = _catalogCacheManager.CatalogDocument?.Services
                .Where(x => viewModel.CommandLineArgumentModel.SelectedServices.Contains(x.Id))
                .FirstOrDefault();

            viewModel.SelectedService = commandLineSelectedService;
        }

        return viewModel;
    }
}

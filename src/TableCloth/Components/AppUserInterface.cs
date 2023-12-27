using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using TableCloth.Contracts;
using TableCloth.Dialogs;
using TableCloth.Models;
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

    public CatalogPage CreateCatalogPage(CatalogPageArgumentModel argumentModel)
        => new CatalogPage(CreateCatalogPageViewModel(argumentModel));

    public CatalogPageViewModel CreateCatalogPageViewModel(CatalogPageArgumentModel argumentModel)
    {
        var viewModel = _serviceProvider.GetRequiredService<CatalogPageViewModel>();
        viewModel.SearchKeyword = argumentModel.SearchKeyword;
        viewModel.PageArgument = argumentModel;
        return viewModel;
    }

    public DetailPage CreateDetailPage(ITableClothArgumentModel argumentModel)
        => new DetailPage(CreateDetailPageViewModel(argumentModel));

    public DetailPageViewModel CreateDetailPageViewModel(ITableClothArgumentModel argumentModel)
    {
        var viewModel = _serviceProvider.GetRequiredService<DetailPageViewModel>();

        var selectedService = _catalogCacheManager.CatalogDocument?.Services
            .Where(x => argumentModel.SelectedServices.Contains(x.Id))
            .FirstOrDefault();

        viewModel.SelectedService = selectedService;
        viewModel.PageArgument = argumentModel;
        return viewModel;
    }
}

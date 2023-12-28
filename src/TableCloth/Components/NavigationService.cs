using System;
using System.Windows.Controls;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public sealed class NavigationService
{
    private readonly AppUserInterface _appUserInterface;

    private Frame? _frame = default;

    public NavigationService(
        AppUserInterface appUserInterface)
    {
        _appUserInterface = appUserInterface;
    }

    public void Initialize(Frame frame)
    {
        _frame = frame ?? throw new ArgumentNullException(nameof(frame));
    }

    public bool NavigateToCatalog(string searchKeyword)
    {
        if (_frame == default)
            throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

        var page = _appUserInterface.CreateCatalogPage(searchKeyword);
        return _frame.Navigate(page);
    }

    public bool NavigateToDetail(
        CatalogInternetService selectedService,
        CommandLineArgumentModel? commandLineArgumentModel)
    {
        if (_frame == default)
            throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

        var page = _appUserInterface.CreateDetailPage(selectedService, commandLineArgumentModel);
        return _frame.Navigate(page);
    }

    public bool GoBack()
    {
        if (_frame == default)
            throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

        if (!_frame.CanGoBack)
            return false;

        _frame.GoBack();
        return true;
    }

    public bool GoForward()
    {
        if (_frame == default)
            throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

        if (!_frame.CanGoForward)
            return false;

        _frame.GoForward();
        return true;
    }

    public void Refresh()
    {
        if (_frame == default)
            throw new InvalidOperationException($"You should initialize {nameof(NavigationService)} before use.");

        _frame.Refresh();
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Diagnostics;
using System.Linq;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Pages;

public partial class CatalogPage : UserControl
{
    public CatalogPage() => InitializeComponent();

    public CatalogPage(
        CatalogPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    public CatalogPageViewModel ViewModel
        => (CatalogPageViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.CatalogPageLoadedCommand.CanExecute(ViewModel))
            ViewModel.CatalogPageLoadedCommand.Execute(ViewModel);
    }

    // 이슈 #296: 카드 클릭/더블클릭 → 선택 + (더블클릭 시) 상세 이동. WPF ListView 히트테스트/스크롤 로직은 간소화.
    private void CatalogItem_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: CatalogInternetService service })
            ViewModel.SelectedService = service;
    }

    private void CatalogItem_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control { DataContext: CatalogInternetService service })
            return;

        ViewModel.SelectedService = service;
        if (ViewModel.CatalogPageItemSelectCommand.CanExecute(null))
            ViewModel.CatalogPageItemSelectCommand.Execute(null);
    }

    private void CategoryRadioButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: CatalogInternetServiceCategory category })
            return;

        // WPF 는 해당 카테고리로 스크롤했으나, Avalonia 그룹 뷰에서는 해당 카테고리의 첫 항목을 선택하는 것으로 간소화.
        var first = ViewModel.Services.FirstOrDefault(x => x.Category == category);
        if (first != null)
            ViewModel.SelectedService = first;
    }

    private void SponsorBanner_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://yourtablecloth.app/#sponsor",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Pages;

/// <summary>
/// Interaction logic for DetailPage.xaml
/// </summary>
public partial class DetailPage : Page
{
    public DetailPage(
        DetailPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += ViewModel_CloseRequested;
    }

    private void ViewModel_CloseRequested(object? sender, EventArgs e)
    {
        Window.GetWindow(this).Close();
    }

    public DetailPageViewModel ViewModel
        => (DetailPageViewModel)DataContext;

    // https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox
    private void SiteCatalogFilter_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox siteCatalogFilter)
            return;

        // Fixes issue when clicking cut/copy/paste in context menu
        if (siteCatalogFilter.SelectionLength < 1)
            siteCatalogFilter.SelectAll();
    }

    private void SiteCatalogFilter_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox siteCatalogFilter)
            return;

        if (e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab)
            siteCatalogFilter.TryLeaveFocus();
    }
}

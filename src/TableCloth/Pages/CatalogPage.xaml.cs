using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Pages;

public partial class CatalogPage : Page
{
    public CatalogPage(
        CatalogPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public CatalogPageViewModel ViewModel
        => (CatalogPageViewModel)DataContext;

    // https://stackoverflow.com/questions/1077397/scroll-listviewitem-to-be-at-the-top-of-a-listview
    private static DependencyObject? GetScrollViewer(DependencyObject o)
    {
        if (o is ScrollViewer)
            return o;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
        {
            var child = VisualTreeHelper.GetChild(o, i);
            var result = GetScrollViewer(child);

            if (result == null)
                continue;
            else
                return result;
        }

        return null;
    }

    // https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox
    private void SiteCatalogFilter_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox siteCatalogFilter)
            return;

        // Fixes issue when clicking cut/copy/paste in context menu
        if (siteCatalogFilter.SelectionLength < 1)
            siteCatalogFilter.SelectAll();
    }

    private void SiteCatalogFilter_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (sender is not TextBox siteCatalogFilter)
            return;

        // If user highlights some text, don't override it
        if (siteCatalogFilter.SelectionLength < 1)
            siteCatalogFilter.SelectAll();

        // further clicks will not select all
        siteCatalogFilter.LostMouseCapture -= SiteCatalogFilter_LostMouseCapture;
    }

    private void SiteCatalogFilter_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox siteCatalogFilter)
            return;

        // once we've left the TextBox, return the select all behavior
        siteCatalogFilter.LostMouseCapture += SiteCatalogFilter_LostMouseCapture;
    }

    private void SiteCatalog_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var r = VisualTreeHelper.HitTest(this, e.GetPosition(this));

        if (r.VisualHit is Image || r.VisualHit is TextBlock)
        {
            var elem = (FrameworkElement)r.VisualHit;
            var context = elem.DataContext as CatalogInternetService;
            var hitTestServiceId = context?.Id;

            if (string.IsNullOrWhiteSpace(hitTestServiceId) ||
                !string.Equals(hitTestServiceId, ViewModel.SelectedService?.Id, StringComparison.Ordinal))
            {
                SiteCatalog.UnselectAll();
            }
        }
        else
            SiteCatalog.UnselectAll();
    }

    private void CategoryRadioButton_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as CatalogInternetServiceCategory?;

        if (!tag.HasValue)
            return;

        foreach (var eachItem in SiteCatalog.Items)
        {
            if (eachItem is not CatalogInternetService catalogItem)
                continue;

            if (catalogItem.Category != tag.Value)
                continue;

            SiteCatalog.SelectedItem = eachItem;

            if (GetScrollViewer(SiteCatalog) is ScrollViewer viewer)
                viewer.ScrollToBottom();

            SiteCatalog.ScrollIntoView(eachItem);
            break;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(nameof(CatalogPageViewModel.SearchKeyword), e.PropertyName, StringComparison.Ordinal))
            CollectionViewSource.GetDefaultView(SiteCatalog.ItemsSource).Refresh();
    }

    private void UpdateLabelPopup()
    {
        var selectedItem = SiteCatalog.SelectedItem;
        LabelPopup.IsOpen = false;

        if (selectedItem == null)
            return;

        var selectedItemContainer = (ListViewItem)SiteCatalog.ItemContainerGenerator.ContainerFromItem(selectedItem);

        var textBlock = selectedItemContainer.FindChildControl<TextBlock>();

        if (textBlock != null)
        {
            LabelPopup.PlacementTarget = textBlock;
            LabelPopup.VerticalOffset = 0;
            LabelPopup.HorizontalOffset = 0;
            LabelPopup.Placement = PlacementMode.RelativePoint;
            LabelPopup.Width = textBlock.ActualWidth;
            LabelPopup.Height = textBlock.ActualHeight;

            LabelPopupTextBlock.Text = textBlock.Text;
            LabelPopupTextBlock.TextTrimming = TextTrimming.None;
            LabelPopupTextBlock.TextWrapping = TextWrapping.Wrap;
            LabelPopupTextBlock.TextAlignment = textBlock.TextAlignment;
            LabelPopupTextBlock.Background = SystemColors.ControlBrush;
            LabelPopupTextBlock.Foreground = SystemColors.ControlTextBrush;

            LabelPopup.IsOpen = textBlock.IsControlVisibleToUser(SiteCatalog);
        }
        else
            LabelPopup.IsOpen = false;
    }

    private void SiteCatalog_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateLabelPopup();
    }

    private void SiteCatalog_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateLabelPopup();
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Pages
{
    public partial class CatalogPage : Page
    {
        public CatalogPage()
        {
            InitializeComponent();
        }

        public CatalogPageViewModel ViewModel
            => (CatalogPageViewModel)DataContext;

        private static readonly PropertyGroupDescription GroupDescription =
            new PropertyGroupDescription(nameof(CatalogInternetService.CategoryDisplayName));

        // https://stackoverflow.com/questions/1077397/scroll-listviewitem-to-be-at-the-top-of-a-listview
        private DependencyObject GetScrollViewer(DependencyObject o)
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

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var view = (CollectionView)CollectionViewSource.GetDefaultView(ViewModel.Services);
            view.Filter = SiteCatalog_Filter;

            if (!view.GroupDescriptions.Contains(GroupDescription))
                view.GroupDescriptions.Add(GroupDescription);

            var extraArg = ViewModel.ExtraArgument as CatalogPageArgumentModel;
            SiteCatalogFilter.Text = extraArg?.SearchKeyword ?? string.Empty;
        }

        private bool SiteCatalog_Filter(object item)
        {
            var actualItem = item as CatalogInternetService;

            if (actualItem == null)
                return false;

            var filterText = SiteCatalogFilter.Text;

            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            var result = false;
            var splittedFilterText = filterText.Split(new char[] { ',', }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var eachFilterText in splittedFilterText)
            {
                result |= actualItem.DisplayName.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                    || actualItem.CategoryDisplayName.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                    || actualItem.Url.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                    || actualItem.Packages.Count.ToString().Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                    || actualItem.Packages.Any(x => x.Name.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase))
                    || actualItem.Id.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }

        private void SiteCatalogFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(SiteCatalog.ItemsSource).Refresh();
        }

        // https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox
        private void SiteCatalogFilter_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Fixes issue when clicking cut/copy/paste in context menu
            if (SiteCatalogFilter.SelectionLength < 1)
                SiteCatalogFilter.SelectAll();
        }

        private void SiteCatalogFilter_LostMouseCapture(object sender, MouseEventArgs e)
        {
            // If user highlights some text, don't override it
            if (SiteCatalogFilter.SelectionLength < 1)
                SiteCatalogFilter.SelectAll();

            // further clicks will not select all
            SiteCatalogFilter.LostMouseCapture -= SiteCatalogFilter_LostMouseCapture;
        }

        private void SiteCatalogFilter_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // once we've left the TextBox, return the select all behavior
            SiteCatalogFilter.LostMouseCapture += SiteCatalogFilter_LostMouseCapture;
        }

        private void SiteCatalog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                var data = SiteCatalog.SelectedItem as CatalogInternetService;

                if (data != null)
                {
                    ViewModel.NavigationService.NavigateTo<DetailPageViewModel>(
                        new DetailPageArgumentModel(data, builtFromCommandLine: false, currentSearchString: SiteCatalogFilter.Text));
                }
            }
        }

        private void SiteCatalog_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var r = VisualTreeHelper.HitTest(this, e.GetPosition(this));

            if (r.VisualHit.GetType() != typeof(ListBoxItem))
                SiteCatalog.UnselectAll();
        }

        private void SiteCatalog_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var r = VisualTreeHelper.HitTest(this, e.GetPosition(this));
            var data = (r.VisualHit as FrameworkElement)?.DataContext as CatalogInternetService;

            if (data != null)
            {
                ViewModel.NavigationService.NavigateTo<DetailPageViewModel>(
                    new DetailPageArgumentModel(data, builtFromCommandLine: false, currentSearchString: SiteCatalogFilter.Text));
            }
        }

        private void CategoryRadioButton_Click(object sender, RoutedEventArgs e)
        {
            var tag = (sender as FrameworkElement)?.Tag as CatalogInternetServiceCategory?;

            if (!tag.HasValue)
                return;

            foreach (var eachItem in SiteCatalog.Items)
            {
                var catalogItem = eachItem as CatalogInternetService;

                if (catalogItem == null)
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
    }
}

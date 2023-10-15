using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using TableCloth.Contracts;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Pages
{
    public partial class CatalogPage : Page, IPageArgument<CatalogPageArgumentModel>
    {
        public CatalogPage()
        {
            InitializeComponent();
        }

        public CatalogPageViewModel ViewModel
            => (CatalogPageViewModel)DataContext;

        private static readonly PropertyGroupDescription GroupDescription =
            new PropertyGroupDescription(nameof(CatalogInternetService.CategoryDisplayName));

        public CatalogPageArgumentModel Arguments { get; set; } = default;

        private UIElement CreateCategoryButton(CatalogInternetServiceCategory val)
        {
            var displayName = val.GetType().GetField(val.ToString())
                ?.GetCustomAttribute<EnumDisplayNameAttribute>()
                ?.DisplayName ?? val.ToString();

            var button = new RadioButton()
            {
                Content = displayName,
                Tag = val,
                Margin = new Thickness(8d),
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
            };
            return button;
        }

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

            var tupleList = new List<Tuple<CatalogInternetServiceCategory, int>>();

            foreach (var eachMember in Enum.GetValues<CatalogInternetServiceCategory>())
            {
                var order = eachMember.GetType().GetField(eachMember.ToString())
                    ?.GetCustomAttribute<EnumDisplayOrderAttribute>()
                    ?.Order ?? 0;

                tupleList.Add(new(eachMember, order));
            }

            CategoryButtonList.Children.Clear();

            foreach (var eachMember in tupleList.OrderBy(x => x.Item2).Select(x => x.Item1))
                CategoryButtonList.Children.Add(CreateCategoryButton(eachMember));

            SiteCatalogFilter.Text = Arguments?.SearchKeyword ?? string.Empty;
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

        private void SiteCatalogFilter_LostTouchCapture(object sender, TouchEventArgs e)
        {
            // If user highlights some text, don't override it
            if (SiteCatalogFilter.SelectionLength < 1)
                SiteCatalogFilter.SelectAll();

            // further clicks will not select all
            SiteCatalogFilter.LostTouchCapture -= SiteCatalogFilter_LostTouchCapture;
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
            SiteCatalogFilter.LostTouchCapture += SiteCatalogFilter_LostTouchCapture;
        }

        private void SiteCatalog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                var data = SiteCatalog.SelectedItem as CatalogInternetService;

                if (data != null)
                {
                    NavigationService.Navigate(
                        new Uri("Pages/DetailPage.xaml", UriKind.Relative),
                        new DetailPageArgumentModel(data, builtFromCommandLine: false));
                }
            }
        }

        private void SiteCatalog_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var r = VisualTreeHelper.HitTest(this, e.GetPosition(this));

            if (r.VisualHit.GetType() != typeof(ListBoxItem))
                SiteCatalog.UnselectAll();
        }

        private void SiteCatalog_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var r = VisualTreeHelper.HitTest(this, e.GetPosition(this));
            var data = (r.VisualHit as FrameworkElement)?.DataContext as CatalogInternetService;

            if (data != null)
            {
                NavigationService.Navigate(
                    new Uri("Pages/DetailPage.xaml", UriKind.Relative),
                    new DetailPageArgumentModel(data, builtFromCommandLine: false));
            }
        }

        private void ReloadCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AppRestartManager.RestartNow();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow() { Owner = Window.GetWindow(this), };
            aboutWindow.ShowDialog();
        }

        // from https://stackoverflow.com/questions/19397780/scrollviewer-indication-of-child-element-scrolled-into-view
        private bool IsUserVisible(FrameworkElement element, FrameworkElement container)
        {
            if (!(element?.IsVisible ?? false))
                return false;

            Rect bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
            Rect rect = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);
            return rect.Contains(bounds.TopLeft) || rect.Contains(bounds.BottomRight);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public MainWindowViewModel ViewModel
            => (MainWindowViewModel)DataContext;

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var certSelectWindow = ViewModel.AppUserInterface.CreateWindow<CertSelectWindow>(window =>
            {
                window.Owner = this;
            });
            var response = certSelectWindow.ShowDialog();

            if (!response.HasValue || !response.Value)
                return;

            if (certSelectWindow.ViewModel.SelectedCertPair != null)
                ViewModel.SelectedCertFile = certSelectWindow.ViewModel.SelectedCertPair;
        }

        private void SiteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)e.Source;
            ViewModel.SelectedServices = listBox.SelectedItems.Cast<CatalogInternetService>().ToList();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
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

        #region Sort Support

        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection? _lastDirection = null;

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection? direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else if (_lastDirection == null)
                        {
                            direction = ListSortDirection.Ascending;
                        }
                        else
                        {
                            direction = null;
                        }
                    }

                    //var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                    //var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;
                    var column = headerClicked.Column as ExtendedGridViewColumn;
                    var sortBy = column?.BindingPath ?? headerClicked.Column.Header as string;

                    Sort(sortBy, direction);

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else if (direction == ListSortDirection.Descending)
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate = null;
                    }

                    // Remove arrow from previously sorted header
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                    {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }
        private void Sort(string sortBy, ListSortDirection? direction)
        {
            var dataView = CollectionViewSource.GetDefaultView(SiteCatalog.ItemsSource);
            dataView.SortDescriptions.Clear();

            if (direction != null)
            {
                SortDescription sd = new SortDescription(sortBy, direction ?? ListSortDirection.Ascending);
                dataView.SortDescriptions.Add(sd);
            }
            dataView.Refresh();
        }

        #endregion Sort Support
    }
}

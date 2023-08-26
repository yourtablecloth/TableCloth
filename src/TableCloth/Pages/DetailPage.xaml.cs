using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;

namespace TableCloth.Pages
{
    /// <summary>
    /// Interaction logic for DetailPage.xaml
    /// </summary>
    public partial class DetailPage : Page, IPageArgument<CatalogInternetService>
    {
        public DetailPage()
        {
            InitializeComponent();
        }

        public IEnumerable<CatalogInternetService> Arguments { get; set; } = default;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void GoBackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace TableCloth.Pages
{
    /// <summary>
    /// Interaction logic for DetailPage.xaml
    /// </summary>
    public partial class DetailPage : Page
    {
        public DetailPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void GoBackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(
                new Uri("Pages/CatalogPage.xaml", UriKind.Relative),
                null);
        }
    }
}

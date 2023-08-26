using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TableCloth.Models.Catalog;

namespace TableCloth.Pages
{
    /// <summary>
    /// Interaction logic for CatalogPage.xaml
    /// </summary>
    public partial class CatalogPage : Page
    {
        public CatalogPage()
        {
            InitializeComponent();

            var view = (CollectionView)CollectionViewSource.GetDefaultView(SiteCatalog.ItemsSource);
            var groupDescription = new PropertyGroupDescription(nameof(CatalogInternetService.Category));
            view.GroupDescriptions.Add(groupDescription);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Uri("Pages/DetailPage.xaml", UriKind.Relative));
        }
    }
}

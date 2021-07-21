using System;
using System.Linq;
using System.Windows;
using TableCloth.Resources;

namespace Host
{
    public partial class MainWindow : Window
    {
        public MainWindow()
            : base()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var catalog = App.Current.GetCatalogDocument();
            var targets = App.Current.GetInstallTargets();

            foreach (var eachTarget in targets)
            {
                var targetService = catalog.Services.Where(x => string.Equals(eachTarget, x.Id, StringComparison.Ordinal)).FirstOrDefault();

                if (targetService == null)
                    continue;

                var packages = targetService.Packages;

                foreach (var eachPackage in packages)
                    InstallList.Items.Add(eachPackage);
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, StringResources.AboutDialog_BodyText, StringResources.AppName, MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
        }

        private void PerformInstallButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

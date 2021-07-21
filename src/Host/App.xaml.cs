using System.IO;
using System.Linq;
using System.Windows;
using TableCloth.Helpers;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Host
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            string catalogXmlFilePath = e.Args.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(catalogXmlFilePath) ||
                !File.Exists(catalogXmlFilePath))
            {
                _ = MessageBox.Show(StringResources.HostError_Cannot_Load_Local_Catalog, StringResources.TitleText_Error,
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                Current.Shutdown(0);
                return;
            }

            string[] installTargets = e.Args.Skip(1).ToArray();

            if (!installTargets.Any())
            {
                _ = MessageBox.Show(StringResources.Host_No_Targets, StringResources.TitleText_Error,
                    MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
                Current.Shutdown(0);
                return;
            }

            using FileStream localCatalogFile = File.OpenRead(catalogXmlFilePath);
            CatalogDocument catalog = XmlHelpers.DeserializeFromXml<CatalogDocument>(localCatalogFile);

            Current.InitCatalogDocument(catalog);
            Current.InitInstallTargets(installTargets);
        }
    }
}

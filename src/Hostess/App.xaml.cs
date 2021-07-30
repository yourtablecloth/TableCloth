using System;
using System.Linq;
using System.Net;
using System.Windows;
using System.Xml;
using TableCloth.Helpers;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Hostess
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            using (var webClient = new WebClient())
            {
                CatalogDocument catalog = null;

                try
                {
                    using (var catalogStream = webClient.OpenRead(StringResources.CatalogUrl))
                    {
                        catalog = XmlHelpers.DeserializeFromXml<CatalogDocument>(catalogStream);

                        if (catalog == null)
                        {
                            throw new XmlException(StringResources.HostessError_CatalogDeserilizationFailure);
                        }

                        Current.InitCatalogDocument(catalog);
                    }
                }
                catch (Exception ex)
                {
                    _ = MessageBox.Show(StringResources.HostessError_CatalogLoadFailure(ex), StringResources.TitleText_Error,
                        MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    Current.Shutdown(0);
                    return;
                }

                var targetSites = e.Args.ToArray();

                if (!targetSites.Any())
                {
                    _ = MessageBox.Show(StringResources.Hostess_No_Targets, StringResources.TitleText_Error,
                        MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
                    Current.Shutdown(0);
                    return;
                }

                Current.InitInstallSites(targetSites);
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
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
                        catalog = DeserializeFromXml<CatalogDocument>(catalogStream);

                        if (catalog == null)
                        {
                            throw new XmlException(StringResources.HostessError_CatalogDeserilizationFailure);
                        }

                        Current.InitCatalogDocument(catalog);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(StringResources.HostessError_CatalogLoadFailure(ex), StringResources.TitleText_Error,
                        MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                    Current.Shutdown(0);
                    return;
                }

                var targetSites = e.Args.ToArray();
                Current.InitInstallSites(targetSites);

                if (!targetSites.Any())
                {
                    MessageBox.Show(StringResources.Hostess_No_Targets, StringResources.TitleText_Error,
                        MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
                    Current.Shutdown(0);
                    return;
                }
            }
        }

        private static T DeserializeFromXml<T>(Stream readableStream)
            where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            var xmlReaderSetting = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
            };

            using (var contentStream = XmlReader.Create(readableStream, xmlReaderSetting))
            {
                return (T)serializer.Deserialize(contentStream);
            }
        }
    }
}

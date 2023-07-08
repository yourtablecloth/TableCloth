using System;
using System.Diagnostics;
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
            try
            {
                using (var webClient = new WebClient())
                {
                    CatalogDocument catalog = null;

                    try
                    {
                        using (var catalogStream = webClient.OpenRead(StringResources.CatalogUrl))
                        {
                            var lastModifiedValue = webClient.ResponseHeaders.Get("Last-Modified");
                            catalog = DeserializeFromXml<CatalogDocument>(catalogStream);

                            if (catalog == null)
                            {
                                throw new XmlException(StringResources.HostessError_CatalogDeserilizationFailure);
                            }

                            Current.InitCatalogDocument(catalog);
                            Current.InitCatalogLastModified(lastModifiedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(StringResources.HostessError_CatalogLoadFailure(ex), StringResources.TitleText_Error,
                            MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                        Current.Shutdown(0);
                        return;
                    }

                    IEModeListDocument ieModeList = null;

                    try
                    {
                        using (var ieModeListStream = webClient.OpenRead(StringResources.IEModeListUrl))
                        {
                            var lastModifiedValue = webClient.ResponseHeaders.Get("Last-Modified");
                            ieModeList = DeserializeFromXml<IEModeListDocument>(ieModeListStream);

                            if (catalog == null)
                            {
                                throw new XmlException(StringResources.HostessError_CatalogDeserilizationFailure);
                            }

                            Current.InitIEModeListDocument(ieModeList);
                            Current.InitIEModeListLastModified(lastModifiedValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(StringResources.HostessError_CatalogLoadFailure(ex), StringResources.TitleText_Error,
                            MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK);
                        Current.Shutdown(0);
                        return;
                    }

                    var targetSites = e.Args.Where(x => !x.StartsWith(StringResources.TableCloth_Switch_Prefix, StringComparison.Ordinal)).ToArray();
                    Current.InitInstallSites(targetSites);

                    var installEveryonesPrinter = false;
                    var installAdobeReader = false;
                    var installHancomOfficeViewer = false;
                    var installRaiDrive = true;
                    var hasIEModeEnabled = false;
                    var showHelp = false;

                    var options = e.Args.Where(x => x.StartsWith(StringResources.TableCloth_Switch_Prefix, StringComparison.Ordinal)).ToArray();
                    foreach (var eachOption in options)
                    {
                        if (eachOption.StartsWith(StringResources.TableCloth_Switch_InstallEveryonesPrinter, StringComparison.Ordinal))
                            installEveryonesPrinter = true;

                        if (eachOption.StartsWith(StringResources.TableCloth_Switch_InstallAdobeReader, StringComparison.Ordinal))
                            installAdobeReader = true;

                        if (eachOption.StartsWith(StringResources.TableCloth_Switch_InstallHancomOfficeViewer, StringComparison.Ordinal))
                            installHancomOfficeViewer = true;

                        if (eachOption.StartsWith(StringResources.TableCloth_Switch_InstallRaiDrive, StringComparison.Ordinal))
                            installRaiDrive = true;

                        if (eachOption.StartsWith(StringResources.TableCloth_Switch_EnableIEMode, StringComparison.Ordinal))
                            hasIEModeEnabled = true;

                        if (eachOption.StartsWith(StringResources.TableCloth_Switch_Help, StringComparison.Ordinal))
                            showHelp = true;
                    }

                    if (showHelp)
                    {
                        MessageBox.Show(StringResources.TableCloth_Hostess_Switches_Help, StringResources.TitleText_Info,
                            MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
                        Current.Shutdown(0);
                        return;
                    }

                    Current.InitWillInstallEveryonesPrinter(installEveryonesPrinter);
                    Current.InitWillInstallAdobeReader(installAdobeReader);
                    Current.InitWillInstallHancomOfficeViewer(installHancomOfficeViewer);
                    Current.InitWillInstallRaiDrive(installRaiDrive);
                    Current.InitHasIEModeEnabled(hasIEModeEnabled);

                    if (!targetSites.Any())
                    {
                        MessageBox.Show(StringResources.Hostess_No_Targets, StringResources.TitleText_Error,
                            MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);

                        Process.Start(new ProcessStartInfo("https://www.naver.com/")
                        {
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Maximized,
                        });

                        Current.Shutdown(0);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
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

using System.Collections.Generic;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

public interface ITableClothViewModel
{
    bool EnableMicrophone { get; }
    bool EnableWebCam { get; }
    bool EnablePrinters { get; }
    bool InstallEveryonesPrinter { get; }
    bool InstallAdobeReader { get; }
    bool InstallHancomOfficeViewer { get; }
    bool InstallRaiDrive { get; }
    bool MapNpkiCert { get; }
    IEnumerable<CatalogInternetService> SelectedServices { get; }
    X509CertPair? SelectedCertFile { get; set; }
}

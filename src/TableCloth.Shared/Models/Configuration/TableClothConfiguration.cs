using System;
using System.Collections.Generic;
using TableCloth.Models.Catalog;

namespace TableCloth.Models.Configuration
{
    [Serializable]
    public sealed class TableClothConfiguration
    {
        public X509CertPair CertPair { get; set; }
        public bool EnableMicrophone { get; set; }
        public bool EnableWebCam { get; set; }
        public bool EnablePrinters { get; set; }
        public bool EnableEveryonesPrinter { get; set; }
        public bool EnableAdobeReader { get; set; }
        public bool EnableHancomOfficeViewer { get; set; }
        public bool EnableInternetExplorerMode { get; set; }
        public ICollection<CatalogCompanion> Companions { get; set; }
        public ICollection<CatalogInternetService> Services { get; set; }
        public string AssetsDirectoryPath { get; internal set; }
    }
}

using System;
using System.Collections.Generic;
using TableCloth.Models.Catalog;

namespace TableCloth.Models.Configuration
{
    [Serializable]
    public sealed class TableClothConfiguration
    {
        public X509CertPair CertPair { get; init; }
        public bool EnableMicrophone { get; init; }
        public bool EnableWebCam { get; init; }
        public bool EnablePrinters { get; init; }
        public ICollection<CatalogInternetService> Packages { get; init; }
        public string AssetsDirectoryPath { get; internal set; }
    }
}

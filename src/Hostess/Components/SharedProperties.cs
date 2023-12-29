using System;
using System.Collections.Generic;
using System.Windows;
using TableCloth.Models.Catalog;

namespace Hostess.Components
{
    public sealed class SharedProperties
    {
        public CatalogDocument GetCatalogDocument()
            => GetAppProperty<CatalogDocument>("Catalog");

        public void InitCatalogDocument(CatalogDocument value)
            => InitAppProperty("Catalog", value);

        public string GetCatalogLastModified()
            => GetAppProperty<string>("CatalogLastModified");

        public void InitCatalogLastModified(string value)
            => InitAppProperty("CatalogLastModified", value);

        public string GetIEModeListLastModified()
            => GetAppProperty<string>("IEModeListLastModified");

        public void InitIEModeListLastModified(string value)
            => InitAppProperty("IEModeListLastModified", value);

        public IEnumerable<string> GetInstallSites()
            => GetAppProperty<IEnumerable<string>>("InstallSites");

        public void InitInstallSites(IEnumerable<string> value)
            => InitAppProperty("InstallSites", value);

        public bool WillInstallEveryonesPrinter()
            => string.Equals(Boolean.TrueString, GetAppProperty<string>(nameof(WillInstallEveryonesPrinter)));

        public void InitWillInstallEveryonesPrinter(bool value)
            => InitAppProperty(nameof(WillInstallEveryonesPrinter), value ? Boolean.TrueString : Boolean.FalseString);

        public bool WillInstallAdobeReader()
            => string.Equals(Boolean.TrueString, GetAppProperty<string>(nameof(WillInstallAdobeReader)));

        public void InitWillInstallAdobeReader(bool value)
            => InitAppProperty(nameof(WillInstallAdobeReader), value ? Boolean.TrueString : Boolean.FalseString);

        public bool WillInstallHancomOfficeViewer()
            => string.Equals(Boolean.TrueString, GetAppProperty<string>(nameof(WillInstallHancomOfficeViewer)));

        public void InitWillInstallHancomOfficeViewer(bool value)
            => InitAppProperty(nameof(WillInstallHancomOfficeViewer), value ? Boolean.TrueString : Boolean.FalseString);

        public bool WillInstallRaiDrive()
            => string.Equals(Boolean.TrueString, GetAppProperty<string>(nameof(WillInstallRaiDrive)));

        public void InitWillInstallRaiDrive(bool value)
            => InitAppProperty(nameof(WillInstallRaiDrive), value ? Boolean.TrueString : Boolean.FalseString);

        public bool GetHasIEModeEnabled()
            => string.Equals(Boolean.TrueString, GetAppProperty<string>("HasIEModeEnabled"));

        public void InitHasIEModeEnabled(bool value)
            => InitAppProperty("HasIEModeEnabled", value ? Boolean.TrueString : Boolean.FalseString);

        private TObject GetAppProperty<TObject>(string key)
            where TObject : class
            => string.IsNullOrWhiteSpace(key)
                ? throw new ArgumentException("Invalid key specified.", nameof(key))
                : !Application.Current.Properties.Contains(key) || !(Application.Current.Properties[key] is TObject @object)
                ? throw new InvalidOperationException("Catalog does not initialized.")
                : @object;

        private void InitAppProperty<TObject>(string key, TObject value)
            where TObject : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Invalid key specified.", nameof(key));

            Application.Current.Properties[key] = !Application.Current.Properties.Contains(key) ? value : throw new InvalidOperationException("Object already initialized");
        }
    }
}

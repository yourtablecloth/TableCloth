using System;
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

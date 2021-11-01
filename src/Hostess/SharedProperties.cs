using System;
using System.Collections.Generic;
using System.Windows;
using TableCloth.Models.Catalog;

namespace Hostess
{
    internal static class SharedProperties
    {
        private static TObject GetAppProperty<TApplication, TObject>(this TApplication app, string key)
            where TApplication : Application
            where TObject : class
            => string.IsNullOrWhiteSpace(key)
                ? throw new ArgumentException("Invalid key specified.", nameof(key))
                : !app.Properties.Contains(key) || !(app.Properties[key] is TObject @object)
                ? throw new InvalidOperationException("Catalog does not initialized.")
                : @object;

        private static void InitAppProperty<TApplication, TObject>(this TApplication app, string key, TObject value)
            where TApplication : Application
            where TObject : class
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Invalid key specified.", nameof(key));

            app.Properties[key] = !app.Properties.Contains(key) ? value : throw new InvalidOperationException("Object already initialized");
        }

        public static CatalogDocument GetCatalogDocument<TApplication>(this TApplication app)
            where TApplication : Application
            => GetAppProperty<TApplication, CatalogDocument>(app, "Catalog");

        public static void InitCatalogDocument<TApplication>(this TApplication app, CatalogDocument value)
            where TApplication : Application
            => InitAppProperty(app, "Catalog", value);

        public static string GetCatalogLastModified<TApplication>(this TApplication app)
            where TApplication : Application
            => GetAppProperty<TApplication, string>(app, "CatalogLastModified");

        public static void InitCatalogLastModified<TApplication>(this TApplication app, string value)
            where TApplication : Application
            => InitAppProperty(app, "CatalogLastModified", value);

        public static IEnumerable<string> GetInstallSites<TApplication>(this TApplication app)
            where TApplication : Application
            => GetAppProperty<TApplication, IEnumerable<string>>(app, "InstallSites");

        public static void InitInstallSites<TApplication>(this TApplication app, IEnumerable<string> value)
            where TApplication : Application
            => InitAppProperty(app, "InstallSites", value);
    }
}

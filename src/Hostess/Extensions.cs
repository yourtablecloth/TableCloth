using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Windows;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Hostess
{
    internal static class Extensions
    {
        public static HttpClient CreateTableClothHttpClient(this IHttpClientFactory httpClientFactory)
        {
            if (httpClientFactory == null)
                throw new ArgumentNullException(nameof(httpClientFactory));

            return httpClientFactory.CreateClient(nameof(ConstantStrings.UserAgentText));
        }

        public static HttpClient CreateGoogleChromeMimickedHttpClient(this IHttpClientFactory httpClientFactory)
        {
            if (httpClientFactory == null)
                throw new ArgumentNullException(nameof(httpClientFactory));

            return httpClientFactory.CreateClient(nameof(ConstantStrings.FamiliarUserAgentText));
        }

        public static IServiceCollection AddWindow<TWindow, TViewModel>(this IServiceCollection services,
            Func<IServiceProvider, TWindow> windowImplementationFactory = default,
            Func<IServiceProvider, TViewModel> viewModelImplementationFactory = default)
            where TWindow : Window
            where TViewModel : class
        {
            if (windowImplementationFactory != null)
                services.AddTransient(windowImplementationFactory);
            else
                services.AddTransient<TWindow>();

            if (viewModelImplementationFactory != null)
                services.AddTransient(viewModelImplementationFactory);
            else
                services.AddTransient<TViewModel>();

            return services;
        }

        public static IServiceProvider GetServiceProvider(this Application application)
            => (IServiceProvider)(application.Properties[nameof(IServiceProvider)] ?? throw new Exception("Service provider has not been initialized."));

        public static void InitServiceProvider(this Application application, IServiceProvider serviceProvider)
        {
            const string key = nameof(IServiceProvider);

            if (application.Properties.Contains(key) &&
                application.Properties[key] != null)
                throw new ArgumentException("Already service provider has been initialized.", nameof(application));

            application.Properties[key] = serviceProvider;
        }

        public static bool HasAnyCompatNotes(this CatalogDocument catalog, IEnumerable<string> targets)
            => catalog.Services.Where(x => targets.Contains(x.Id)).Any(x => !string.IsNullOrWhiteSpace(x.CompatibilityNotes?.Trim()));
    }
}

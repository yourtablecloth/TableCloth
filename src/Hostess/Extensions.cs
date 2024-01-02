using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Windows;

namespace Hostess
{
    internal static class Extensions
    {
        public static HttpClient CreateTableClothHttpClient(this IHttpClientFactory httpClientFactory)
        {
            if (httpClientFactory == null)
                throw new ArgumentNullException(nameof(httpClientFactory));

            return httpClientFactory.CreateClient(nameof(TableCloth));
        }

        public static IServiceCollection AddWindow<TWindow, TViewModel>(this IServiceCollection services,
            Func<IServiceProvider, TWindow> windowImplementationFactory = default,
            Func<IServiceProvider, TViewModel> viewModelImplementationFactory = default)
            where TWindow : Window
            where TViewModel : class
        {
            if (windowImplementationFactory != null)
                services.AddTransient<TWindow>(windowImplementationFactory);
            else
                services.AddTransient<TWindow>();

            if (viewModelImplementationFactory != null)
                services.AddTransient<TViewModel>(viewModelImplementationFactory);
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
    }
}

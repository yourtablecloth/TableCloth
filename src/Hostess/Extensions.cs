using Hostess.Browsers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using TableCloth;
using TableCloth.Resources;

namespace Hostess
{
    internal static class Extensions
    {
        public static ProcessStartInfo CreateWebPageOpenRequest(this IWebBrowserService webBrowserService, string url, ProcessWindowStyle processWindowStyle = default)
        {
            if (!webBrowserService.TryGetBrowserExecutablePath(out var executableFilePath))
                return new ProcessStartInfo(url) { UseShellExecute = true, WindowStyle = processWindowStyle };

            return new ProcessStartInfo(executableFilePath, url) { UseShellExecute = false, WindowStyle = processWindowStyle };
        }

        public static HttpClient CreateTableClothHttpClient(this IHttpClientFactory httpClientFactory)
            => httpClientFactory
                .EnsureArgumentNotNull("HTTP Client Factory cannot be null reference.", nameof(httpClientFactory))
                .CreateClient(nameof(ConstantStrings.UserAgentText));

        public static HttpClient CreateGoogleChromeMimickedHttpClient(this IHttpClientFactory httpClientFactory)
            => httpClientFactory
                .EnsureArgumentNotNull("HTTP Client Factory cannot be null reference.", nameof(httpClientFactory))
                .CreateClient(nameof(ConstantStrings.FamiliarUserAgentText));

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
            => application
                .Properties[nameof(IServiceProvider)]
                .EnsureNotNullWithCast<object, IServiceProvider>("Service provider has not been initialized.");

        public static void InitServiceProvider(this Application application, IServiceProvider serviceProvider)
        {
            const string key = nameof(IServiceProvider);

            if (application.Properties.Contains(key) &&
                application.Properties[key] != null)
                TableClothAppException.Throw("Already service provider has been initialized.");

            application.Properties[key] = serviceProvider;
        }
    }
}

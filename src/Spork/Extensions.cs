using Spork.Browsers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using TableCloth;
using TableCloth.Resources;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace Spork
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

        public static async Task CopyStreamWithProgressAsync(
            this Stream source,
            Stream destination,
            IProgress<double> progress = default,
            int bufferSize = 81920,
            CancellationToken cancellationToken = default)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            if (!source.CanRead)
                throw new ArgumentException("Source stream must be readable.", nameof(source));

            if (!destination.CanWrite)
                throw new ArgumentException("Destination stream must be writable.", nameof(destination));

            var buffer = new byte[bufferSize];
            var totalBytesRead = 0L;
            var totalLength = source.CanSeek ? source.Length : default(long?);

            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;

                if (totalLength.HasValue)
                    progress?.Report((double)totalBytesRead / totalLength.Value);
            }
        }
    }
}

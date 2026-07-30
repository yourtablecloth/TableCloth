using Microsoft.Extensions.DependencyInjection;
using Spork.Browsers;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;
using TableCloth.Resources;

namespace Spork
{
    internal static class Extensions
    {
        public static ProcessStartInfo CreateWebPageOpenRequest(this IWebBrowserService webBrowserService, string url, ProcessWindowStyle processWindowStyle = default)
        {
            if (!webBrowserService.TryGetBrowserExecutablePath(out var executableFilePath))
                return new ProcessStartInfo(url) { UseShellExecute = true, WindowStyle = processWindowStyle };

            // URL 은 Arguments 문자열이 아니라 ArgumentList 로 넘긴다. 카탈로그에서 온 URL 만 열던
            // 시절에는 차이가 없었지만, 무설치 딥링크(--target-url)로 외부에서 온 URL 도 이 경로를
            // 지나가므로 Arguments 문자열 연결은 브라우저 인자 주입 통로가 된다
            // (예: URL 에 공백을 넣어 msedge 스위치를 덧붙이는 형태). ArgumentList 는 Windows 인용
            // 규칙에 맞게 이스케이프하므로 URL 이 항상 인자 하나로 전달된다.
            // CatalogTargetUrlMatcher 도 공백/제어문자/큰따옴표를 미리 거르지만 이중으로 방어한다.
            var startInfo = new ProcessStartInfo(executableFilePath) { UseShellExecute = false, WindowStyle = processWindowStyle };
            startInfo.ArgumentList.Add(url);
            return startInfo;
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

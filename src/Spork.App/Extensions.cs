using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Spork.Browsers;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        // 이슈 #296(트림/AOT): AddTransient<T> 는 DI 활성화를 위해 public 생성자 보존을 요구한다. 제네릭 파라미터에
        // DynamicallyAccessedMembers 를 전파해 트리머가 창/VM 생성자를 제거하지 않도록 보장한다(IL2091 해소).
        public static IServiceCollection AddWindow<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TWindow,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TViewModel>(this IServiceCollection services,
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

        // 이슈 #296: WPF Application.Properties[IServiceProvider] 기반 Init/GetServiceProvider 는 폐기.
        // Avalonia Application 에는 Properties 딕셔너리가 없어 SporkApplication.ServiceProvider 정적 홀더로 대체.

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

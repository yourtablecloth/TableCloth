#nullable enable

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace TableCloth.Controls
{
    /// <summary>
    /// 이슈 #296: WPF <c>ImageBrush ImageSource="{Binding 원격URL}"</c>(원격 URL 자동 다운로드)의 Avalonia 대체.
    /// Avalonia <see cref="Bitmap"/> 은 원격 URI 를 자동 로드하지 않으므로, 첨부 속성으로 URL 을 받아 비동기로
    /// 다운로드·디코드해 <see cref="Image.Source"/> 에 채운다(URL별 캐시). 의존성 추가 없이 About 창의 GitHub
    /// 후원자/기여자 아바타에 사용. netstandard2.0 인 TableCloth.Core 에는 둘 수 없어 src/Shared 공유 링크로 배포.
    /// </summary>
    public static class RemoteImageLoader
    {
        private static readonly HttpClient Http = CreateClient();

        // URL별 1회 다운로드/디코드 후 공유(카탈로그처럼 목록이 재구성돼도 재다운로드 방지). 실패는 null 캐시.
        private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Cache =
            new(StringComparer.Ordinal);

        public static readonly AttachedProperty<string?> SourceUrlProperty =
            AvaloniaProperty.RegisterAttached<Image, string?>("SourceUrl", typeof(RemoteImageLoader));

        static RemoteImageLoader()
        {
            SourceUrlProperty.Changed.AddClassHandler<Image>((image, e) =>
                OnSourceUrlChanged(image, e.GetNewValue<string?>()));
        }

        public static void SetSourceUrl(Image image, string? value) => image.SetValue(SourceUrlProperty, value);

        public static string? GetSourceUrl(Image image) => image.GetValue(SourceUrlProperty);

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15d) };
            // 일부 CDN 은 User-Agent 없는 요청을 거부하므로 최소 UA 를 지정한다(GitHub 아바타 등).
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TableCloth");
            return client;
        }

        private static async void OnSourceUrlChanged(Image image, string? url)
        {
            image.Source = null;

            if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
                return;

            // 다운로드는 UI 스레드에서 시작하되(ConfigureAwait(true)) 완료 후 UI 스레드에서 Source 를 채운다.
            var bitmap = await LoadAsync(url).ConfigureAwait(true);

            // 컨테이너 재사용 등으로 그 사이 URL 이 바뀌었으면 무시(stale 방지).
            if (bitmap != null && string.Equals(GetSourceUrl(image), url, StringComparison.Ordinal))
                image.Source = bitmap;
        }

        private static Task<Bitmap?> LoadAsync(string url)
            => Cache.GetOrAdd(url, static u => DownloadAsync(u));

        private static async Task<Bitmap?> DownloadAsync(string url)
        {
            try
            {
                using var response = await Http.GetAsync(url).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                using var ms = new MemoryStream(bytes);

                // 아바타는 작게 표시되므로 폭 64px 로 디코드(메모리 절약).
                return Bitmap.DecodeToWidth(ms, 64);
            }
            catch
            {
                return null;
            }
        }
    }
}

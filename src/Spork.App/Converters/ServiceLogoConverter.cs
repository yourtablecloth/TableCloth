using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Spork.Converters
{
    /// <summary>
    /// <see cref="CatalogInternetService"/>를 사이트 로고 <see cref="BitmapImage"/>로 변환한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 1순위는 <c>AppContext.BaseDirectory\images\{id}.png</c> 로컬 파일이다. 모드 1(식탁보 + WSB)에서는
    /// 호스트가 Images.zip 을 staging 에 풀어 두므로 이 평면 경로에 로컬 아이콘이 있어 오프라인으로 빠르게 로드된다.
    /// </para>
    /// <para>
    /// 로컬 파일이 없으면(단독 Spork / 무설치 시나리오는 아이콘이 동봉되지 않음) 카탈로그 사이트의
    /// <c>{ConstantStrings.ImageUrlPrefix}/{Category}/{Id}.png</c> 에서 원격으로 비동기 다운로드한다.
    /// 서버 이미지는 카테고리 폴더로 정리돼 있어 <see cref="CatalogInternetService.Category"/> 세그먼트가
    /// 반드시 필요하다(로컬은 평면 <c>{Id}.png</c>). 호스트 측 <c>ResourceResolver.LoadSiteImagesAsync</c> 와 동일 규칙.
    /// 원격 로드는 UI 스레드를 막지 않으며(WPF BitmapImage 원격 URI), 완료되면 바인딩된 Image 가 자동 갱신된다.
    /// </para>
    /// <para>
    /// 어느 경로든 ID별로 1회만 만들어 캐시한다(카탈로그가 그룹/필터 재구성될 때마다 다시 디코드/다운로드하지
    /// 않도록). 원격 다운로드가 실패하면 캐시에서 제거해, 카탈로그를 동적으로 새로고침할 때 다시 시도한다.
    /// </para>
    /// <para>
    /// XAML 바인딩은 ID 문자열이 아니라 서비스 객체 전체를 넘겨야 한다(Category 가 필요하므로):
    /// <c>Source="{Binding Converter={StaticResource ServiceLogoConverter}}"</c>.
    /// </para>
    /// </remarks>
    public sealed class ServiceLogoConverter : IValueConverter
    {
        private static readonly Lazy<string> ImagesDirectory = new Lazy<string>(() =>
        {
            // 단일 파일 게시에서도 안전한 AppContext.BaseDirectory 사용
            return Path.Combine(AppContext.BaseDirectory, "images");
        });

        // 로컬(frozen) 또는 원격(다운로드 중/완료) BitmapImage 를 ID별로 캐시. 변환기는 UI 스레드에서만
        // 호출되므로 같은 인스턴스를 여러 Image 가 공유해도 안전하다.
        private static readonly ConcurrentDictionary<string, BitmapImage> Cache =
            new ConcurrentDictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 하위 호환: ID 문자열만 들어오면 로컬 경로만 시도한다(원격은 Category 가 없어 불가).
            var id = (value as CatalogInternetService)?.Id ?? value as string;
            if (string.IsNullOrWhiteSpace(id))
                return null;

            if (Cache.TryGetValue(id, out var cached))
                return cached;

            // 1순위: 로컬 파일(모드 1). 있으면 frozen 으로 1회 디코드 후 캐시.
            try
            {
                var path = Path.Combine(ImagesDirectory.Value, id + ".png");
                if (File.Exists(path))
                {
                    var localBitmap = new BitmapImage();
                    localBitmap.BeginInit();
                    localBitmap.CacheOption = BitmapCacheOption.OnLoad;
                    localBitmap.UriSource = new Uri(path, UriKind.Absolute);
                    localBitmap.EndInit();
                    localBitmap.Freeze();

                    Cache.TryAdd(id, localBitmap);
                    return localBitmap;
                }
            }
            catch
            {
                // 로컬 디코드 실패 시엔 원격으로 폴백.
            }

            // 2순위: 원격(단독 Spork / 무설치). Category 가 있어야 URL 을 만들 수 있다.
            var category = (value as CatalogInternetService)?.Category.ToString();
            if (string.IsNullOrWhiteSpace(category))
                return null;

            return LoadRemote(id, category);
        }

        private static BitmapImage LoadRemote(string id, string category)
        {
            try
            {
                var prefix = ConstantStrings.ImageUrlPrefix;
                if (string.IsNullOrWhiteSpace(prefix))
                    return null;

                // 서버는 카테고리 폴더로 정리됨: {prefix}/{Category}/{Id}.png
                var remoteUri = new Uri($"{prefix.TrimEnd('/')}/{category}/{id}.png", UriKind.Absolute);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = remoteUri;
                bitmap.EndInit();

                if (!bitmap.IsDownloading)
                {
                    // 캐시 히트 등으로 이미 즉시 로드된 경우엔 frozen 으로 공유 안전성 확보.
                    try { bitmap.Freeze(); } catch { /* 일부 상태에선 freeze 불가 — 무시 */ }
                }
                else
                {
                    // 다운로드 실패 시 캐시에서 제거 → 동적 새로고침 때 재시도.
                    bitmap.DownloadFailed += (sender, e) => Cache.TryRemove(id, out _);
                }

                Cache.TryAdd(id, bitmap);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

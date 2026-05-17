using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Spork.Converters
{
    /// <summary>
    /// 사이트 ID(string)를 받아 Spork.exe와 같은 디렉터리의 <c>images\{id}.png</c>를
    /// <see cref="BitmapImage"/>로 로드합니다. 파일이 없으면 null(빈 이미지)을 반환합니다.
    /// 호스트 빌드는 Images.zip을 staging의 assets 폴더로 풀어 두며, 샌드박스에서 Spork.exe는
    /// 같은 폴더 하위에서 실행되므로 상대 경로로 해석할 수 있습니다.
    /// </summary>
    /// <remarks>
    /// Frozen <see cref="BitmapImage"/>는 스레드 안전하게 공유 가능하므로 ID별 1회 디코드 후 캐시한다.
    /// 캐시가 없으면 카탈로그가 그룹/필터 재구성될 때마다 266여 개 PNG가 mounted 호스트 폴더에서
    /// 동기 디코드되어 UI 스레드를 수백 ms~수 초 freeze 시킨다(<c>BitmapCacheOption.OnLoad</c>).
    /// </remarks>
    public sealed class ServiceLogoConverter : IValueConverter
    {
        private static readonly Lazy<string> ImagesDirectory = new Lazy<string>(() =>
        {
            // 단일 파일 게시에서도 안전한 AppContext.BaseDirectory 사용
            return Path.Combine(AppContext.BaseDirectory, "images");
        });

        private static readonly ConcurrentDictionary<string, BitmapImage> Cache =
            new ConcurrentDictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        // null(파일 부재 등으로 디코드 실패) 결과도 캐시해야 매 호출마다 File.Exists를 반복하지 않는다.
        // ConcurrentDictionary는 null 값을 저장할 수 없으므로 별도 set으로 추적.
        private static readonly ConcurrentDictionary<string, byte> NegativeCache =
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var id = value as string;
            if (string.IsNullOrWhiteSpace(id))
                return null;

            if (Cache.TryGetValue(id, out var cached))
                return cached;

            if (NegativeCache.ContainsKey(id))
                return null;

            try
            {
                var path = Path.Combine(ImagesDirectory.Value, id + ".png");

                if (!File.Exists(path))
                {
                    NegativeCache.TryAdd(id, 0);
                    return null;
                }

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                Cache.TryAdd(id, bitmap);
                return bitmap;
            }
            catch
            {
                NegativeCache.TryAdd(id, 0);
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

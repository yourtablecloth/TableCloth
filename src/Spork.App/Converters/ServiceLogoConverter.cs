using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using TableCloth.Models.Catalog;

namespace Spork.Converters
{
    /// <summary>
    /// 이슈 #296: WPF <c>BitmapImage</c> → Avalonia <see cref="Bitmap"/>. <see cref="CatalogInternetService"/>
    /// (또는 ID 문자열)를 사이트 로고 이미지로 변환한다.
    /// </summary>
    /// <remarks>
    /// 1순위는 <c>AppContext.BaseDirectory\images\{id}.png</c> 로컬 파일(모드 1: 식탁보+WSB 는 호스트가 staging 에
    /// Images.zip 을 풀어 둠). WPF 판은 로컬이 없을 때 카탈로그 서버에서 원격 URI 로 자동 비동기 다운로드했으나,
    /// Avalonia <see cref="Bitmap"/> 은 원격 URI 자동 로딩을 하지 않는다. 원격 async 로더는 M3 이월 항목이므로
    /// 1차에는 로컬만 시도하고 없으면 null(아이콘 미표시)을 돌려준다. ID 별 1회 디코드 후 캐시.
    /// </remarks>
    public sealed class ServiceLogoConverter : IValueConverter
    {
        private static readonly Lazy<string> ImagesDirectory = new(() =>
            Path.Combine(AppContext.BaseDirectory, "images"));

        private static readonly ConcurrentDictionary<string, Bitmap?> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var id = (value as CatalogInternetService)?.Id ?? value as string;
            if (string.IsNullOrWhiteSpace(id))
                return null;

            if (Cache.TryGetValue(id, out var cached))
                return cached;

            try
            {
                var path = Path.Combine(ImagesDirectory.Value, id + ".png");
                if (File.Exists(path))
                {
                    var bitmap = new Bitmap(path);
                    Cache.TryAdd(id, bitmap);
                    return bitmap;
                }
            }
            catch
            {
                // 로컬 디코드 실패는 아이콘 미표시로 처리(카탈로그 흐름을 막지 않음).
            }

            // 원격 아바타/로고 async 로딩은 M3 이월. 로컬이 없으면 아이콘 미표시.
            Cache.TryAdd(id, null);
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

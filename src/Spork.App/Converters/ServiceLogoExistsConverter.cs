using Avalonia.Data.Converters;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using TableCloth.Models.Catalog;

namespace Spork.Converters
{
    /// <summary>
    /// 이슈 #296: 로컬 로고 png(<c>images\{id}.png</c>) 존재 여부(bool)를 돌려준다. 로고가 없는 사이트에
    /// 중립적인 지구본 placeholder 를 대신 표시하기 위한 토글용. <see cref="ServiceLogoConverter"/> 와 동일한
    /// 경로 규칙을 쓰며 결과를 ID 별로 캐시한다. <c>ConverterParameter="Not"</c> 이면 결과를 반전(로고 없을 때 true).
    /// </summary>
    public sealed class ServiceLogoExistsConverter : IValueConverter
    {
        private static readonly Lazy<string> ImagesDirectory = new(() =>
            Path.Combine(AppContext.BaseDirectory, "images"));

        private static readonly ConcurrentDictionary<string, bool> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var invert = string.Equals(parameter as string, "Not", StringComparison.OrdinalIgnoreCase);
            var id = (value as CatalogInternetService)?.Id ?? value as string;
            var exists = !string.IsNullOrWhiteSpace(id) && LogoExists(id!);
            return invert ? !exists : exists;
        }

        private static bool LogoExists(string id)
        {
            if (Cache.TryGetValue(id, out var cached))
                return cached;

            bool result;
            try
            {
                result = File.Exists(Path.Combine(ImagesDirectory.Value, id + ".png"));
            }
            catch
            {
                result = false;
            }

            Cache.TryAdd(id, result);
            return result;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

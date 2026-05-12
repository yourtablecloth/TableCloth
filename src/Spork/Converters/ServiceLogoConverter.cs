using System;
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
    public sealed class ServiceLogoConverter : IValueConverter
    {
        private static readonly Lazy<string> ImagesDirectory = new Lazy<string>(() =>
        {
            // 단일 파일 게시에서도 안전한 AppContext.BaseDirectory 사용
            return Path.Combine(AppContext.BaseDirectory, "images");
        });

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var id = value as string;
            if (string.IsNullOrWhiteSpace(id))
                return null;

            try
            {
                var path = Path.Combine(ImagesDirectory.Value, id + ".png");

                if (!File.Exists(path))
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
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

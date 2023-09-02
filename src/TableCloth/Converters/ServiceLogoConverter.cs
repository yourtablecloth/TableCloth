using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Cache;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TableCloth.Components;

namespace TableCloth.Converters
{
    public class ServiceLogoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var locationService = App.Current.Services.GetService(typeof(SharedLocations)) as SharedLocations;

            if (locationService == null)
                return null;

            var serviceId = value as string;

            if (string.IsNullOrWhiteSpace(serviceId))
                return null;

            var targetFilePath = Path.Combine(locationService.GetImageDirectoryPath(), $"{serviceId}.png");

            if (!File.Exists(targetFilePath))
                return null;

            // https://stackoverflow.com/questions/1491383/reloading-an-image-in-wpf
            var _image = new BitmapImage();
            _image.BeginInit();
            _image.CacheOption = BitmapCacheOption.None;
            _image.UriCachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
            _image.CacheOption = BitmapCacheOption.OnLoad;
            _image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            _image.UriSource = new Uri(targetFilePath, UriKind.Absolute);
            _image.EndInit();

            return _image;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

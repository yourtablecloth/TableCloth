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

            return targetFilePath;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

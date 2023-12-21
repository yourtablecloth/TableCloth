using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using TableCloth.Components;

namespace TableCloth.Converters;

public class ServiceLogoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var locationService = App.Current.Services.GetRequiredService<SharedLocations>();
        var serviceId = value as string;

        if (string.IsNullOrWhiteSpace(serviceId))
            throw new ArgumentException(nameof(value));

        string targetFilePath = Path.Combine(locationService.GetImageDirectoryPath(), $"{serviceId}.png");

        if (!File.Exists(targetFilePath))
            throw new FileNotFoundException("File does not exists.", targetFilePath);

        return targetFilePath;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

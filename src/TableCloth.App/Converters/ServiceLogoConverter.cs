using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using TableCloth.Components;

namespace TableCloth.Converters;

public class ServiceLogoConverter : IValueConverter
{
    private IResourceCacheManager? _resourceCacheManager;

    private static BitmapImage GenerateFallbackImageSource()
    {
        using var stream = new MemoryStream(TableCloth.Properties.Resources.SandboxIcon);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = stream;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            return GenerateFallbackImageSource();

        var imageKey = value as string;

        if (string.IsNullOrWhiteSpace(imageKey))
            return GenerateFallbackImageSource();

        if (_resourceCacheManager == null)
        {
            var serviceProvider = Application.Current.GetServiceProvider();
            _resourceCacheManager = serviceProvider.GetRequiredService<IResourceCacheManager>();
        }

        var image = _resourceCacheManager.GetImage(imageKey);

        if (image == null)
            image = GenerateFallbackImageSource();

        return image;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

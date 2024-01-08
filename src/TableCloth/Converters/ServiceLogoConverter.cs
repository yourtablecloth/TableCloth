using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Drawing;
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

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
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

        if (_resourceCacheManager == null)
        {
            var serviceProvider = Application.Current.GetServiceProvider();
            _resourceCacheManager = serviceProvider.GetRequiredService<IResourceCacheManager>();
        }

        return _resourceCacheManager.GetImage((string)value) ?? throw new ArgumentException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows.Data;
using TableCloth.Components;

namespace TableCloth.Converters;

public class ServiceLogoConverter : IValueConverter
{
    private ResourceCacheManager? _resourceCacheManager;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (_resourceCacheManager == null)
            _resourceCacheManager = App.Current.Services.GetRequiredService<ResourceCacheManager>();

        return _resourceCacheManager.GetImage((string)value) ?? throw new ArgumentException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

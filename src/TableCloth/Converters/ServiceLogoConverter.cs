using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Windows.Data;
using TableCloth.Components;

namespace TableCloth.Converters;

public class ServiceLogoConverter : IValueConverter
{
    private CatalogCacheManager? _catalogCacheManager;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (_catalogCacheManager == null)
            _catalogCacheManager = App.Current.Services.GetRequiredService<CatalogCacheManager>();

        return _catalogCacheManager.GetImage((string)value) ?? throw new ArgumentException(nameof(value));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.IO;
using TableCloth.Components;

namespace TableCloth.Converters;

// 이슈 #296: WPF BitmapImage/ImageSource → Avalonia Bitmap. 사이트 ID 를 캐시된 로고 이미지로 변환하며,
// 없으면 내장 SandboxIcon 을 폴백으로 돌려준다.
public sealed class ServiceLogoConverter : IValueConverter
{
    private IResourceCacheManager? _resourceCacheManager;

    private static readonly Lazy<Bitmap> Fallback = new(() =>
        new Bitmap(new MemoryStream(TableCloth.Properties.Resources.SandboxIcon)));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (Design.IsDesignMode)
            return Fallback.Value;

        if (value is not string imageKey || string.IsNullOrWhiteSpace(imageKey))
            return Fallback.Value;

        if (_resourceCacheManager == null)
        {
            var serviceProvider = TableClothApplication.ServiceProvider;
            if (serviceProvider == null)
                return Fallback.Value;
            _resourceCacheManager = serviceProvider.GetRequiredService<IResourceCacheManager>();
        }

        return _resourceCacheManager.GetImage(imageKey) ?? Fallback.Value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

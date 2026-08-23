using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace TableCloth.Converters;

// 이슈 #296: WPF → Avalonia. 읽기 전용(true)이면 연한 파랑, 읽기-쓰기(false)면 연한 주황 배경.
public sealed class BooleanToBackgroundConverter : IValueConverter
{
    private static readonly IBrush ReadOnlyBrush = new SolidColorBrush(Color.FromRgb(200, 220, 255));
    private static readonly IBrush ReadWriteBrush = new SolidColorBrush(Color.FromRgb(255, 220, 200));
    private static readonly IBrush TransparentBrush = new SolidColorBrush(Colors.Transparent);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolValue ? (boolValue ? ReadOnlyBrush : ReadWriteBrush) : TransparentBrush;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

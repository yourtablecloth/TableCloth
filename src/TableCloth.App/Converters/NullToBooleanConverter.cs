using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TableCloth.Converters;

// 이슈 #296: WPF → Avalonia. null 이 아니면 true.
public sealed class NullToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

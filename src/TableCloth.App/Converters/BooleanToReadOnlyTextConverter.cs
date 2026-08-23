using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using TableCloth.Resources;

namespace TableCloth.Converters;

// 이슈 #296: WPF → Avalonia. 읽기 전용/읽기-쓰기 라벨 문자열.
public sealed class BooleanToReadOnlyTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool boolValue
            ? (boolValue ? UIStringResources.MappedFolder_ReadOnlyLabel : UIStringResources.MappedFolder_ReadWriteLabel)
            : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace TableCloth.Converters;

// 이슈 #296: WPF → Avalonia. RadioButton 등에서 enum 값과 파라미터 문자열의 일치 여부를 bool 로 변환.
// https://stackoverflow.com/posts/406798/
public sealed class EnumBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter is not string parameterString)
            return BindingOperations.DoNothing;

        if (!Enum.IsDefined(value.GetType(), value))
            return BindingOperations.DoNothing;

        var parameterValue = Enum.Parse(value.GetType(), parameterString);
        return parameterValue.Equals(value);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string parameterString || value is not bool isChecked || !isChecked)
            return BindingOperations.DoNothing;

        return Enum.Parse(targetType, parameterString);
    }
}

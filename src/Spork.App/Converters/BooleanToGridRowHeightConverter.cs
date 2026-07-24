using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Spork.Converters
{
    // 이슈 #296: WPF IValueConverter → Avalonia IValueConverter. true 면 파라미터로 지정한 높이,
    // false 면 0. (드라이런 알림 행 높이 토글 등에 사용.)
    public sealed class BooleanToGridRowHeightConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool flag && flag)
            {
                var text = System.Convert.ToString(parameter, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text))
                    return GridLength.Parse(text);
            }

            return new GridLength(0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => BindingOperations.DoNothing;
    }
}

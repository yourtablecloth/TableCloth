using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Spork.Converters
{
    // 이슈 #296: WPF → Avalonia. 리스트 항목 폭을 컨테이너 폭에서 세로 스크롤바만큼 뺀 값으로 맞춘다.
    // Avalonia 에는 SystemParameters.VerticalScrollBarWidth 가 없어 Fluent 기본 스크롤바 폭(약 18px)을 상수로 둔다.
    public sealed class WidthConverter : IValueConverter
    {
        private const double ScrollBarWidth = 18d;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double width)
                return Math.Max(0d, width - ScrollBarWidth);

            return BindingOperations.DoNothing;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => BindingOperations.DoNothing;
    }
}

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Spork.Converters
{
    public class StateToBrushConverter : IValueConverter
    {
        // 라이트/다크 배경 양쪽에서 가독성이 확보되는 중간 톤 상태 색.
        // (기존 DarkGreen/DarkRed 는 다크모드 배경(#232323)에서 대비가 너무 낮았다.)
        private static readonly SolidColorBrush InstalledBrush = Freeze(0x2E, 0xA0, 0x43); // green
        private static readonly SolidColorBrush FailedBrush = Freeze(0xE5, 0x48, 0x4D);    // red
        private static readonly SolidColorBrush PendingBrush = Freeze(0xD2, 0x99, 0x22);   // amber

        private static SolidColorBrush Freeze(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = (value is bool?) ? (bool?)value : null;
            return flag.HasValue ? (flag.Value ? InstalledBrush : FailedBrush) : PendingBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}

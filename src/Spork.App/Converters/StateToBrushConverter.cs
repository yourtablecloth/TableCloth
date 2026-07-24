using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Spork.Converters
{
    // 이슈 #296: WPF → Avalonia. 설치 상태(3-state bool?)를 상태 색으로 변환.
    // 라이트/다크 배경 양쪽에서 가독성이 확보되는 중간 톤.
    public sealed class StateToBrushConverter : IValueConverter
    {
        private static readonly IBrush InstalledBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xA0, 0x43)); // green
        private static readonly IBrush FailedBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));    // red
        private static readonly IBrush PendingBrush = new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22));   // amber

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = value as bool?;
            return flag.HasValue ? (flag.Value ? InstalledBrush : FailedBrush) : PendingBrush;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => BindingOperations.DoNothing;
    }
}

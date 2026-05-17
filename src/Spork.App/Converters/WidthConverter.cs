using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Spork.Converters
{
    public class WidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (double)value - SystemParameters.VerticalScrollBarWidth;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}

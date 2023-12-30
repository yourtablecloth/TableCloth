using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Hostess.Converters
{
    // https://stackoverflow.com/a/21884516
    [ValueConversion(typeof(bool), typeof(GridLength))]
    public class BooleanToGridRowHeightConverter : IValueConverter
    {
        private readonly GridLengthConverter _gridLengthConverter = new GridLengthConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => ((bool)value == true) ? _gridLengthConverter.ConvertFromString(System.Convert.ToString(parameter)) : new GridLength(0);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}

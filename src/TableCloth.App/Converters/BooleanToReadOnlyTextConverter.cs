using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TableCloth.Resources;

namespace TableCloth.Converters;

public class BooleanToReadOnlyTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue 
                ? UIStringResources.MappedFolder_ReadOnlyLabel 
                : UIStringResources.MappedFolder_ReadWriteLabel;
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

using System;
using System.Globalization;
using System.Windows.Data;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Implementations.WPF
{
    public class CategoryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is CatalogInternetServiceCategory internalValue) ?
                StringResources.InternetServiceCategory_DisplayText(internalValue) :
                StringResources.InternetService_UnknownText;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

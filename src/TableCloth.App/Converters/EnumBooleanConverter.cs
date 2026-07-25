using System;
using System.Windows;
using System.Windows.Data;

namespace TableCloth.Converters;

// https://stackoverflow.com/posts/406798/

public class EnumBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null)
            return DependencyProperty.UnsetValue;

        if (parameter is not string parameterString)
            return DependencyProperty.UnsetValue;

        if (Enum.IsDefined(value.GetType(), value) == false)
            return DependencyProperty.UnsetValue;

        var parameterValue = Enum.Parse(value.GetType(), parameterString);

        return parameterValue.Equals(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (parameter is not string parameterString)
            return DependencyProperty.UnsetValue;

        // 이슈 #296: 체크된(true) 라디오만 값을 설정한다. 체크 해제(false)에 반응해 파라미터 값을 돌려주면
        // 같은 그룹의 다른 라디오가 해제될 때 값이 잘못 덮어써진다(양방향 바인딩 버그). 기존 OneWay 사용처
        // (CatalogPage)는 ConvertBack 을 호출하지 않으므로 이 수정에 영향받지 않는다.
        if (value is not bool isChecked || !isChecked)
            return Binding.DoNothing;

        return Enum.Parse(targetType, parameterString);
    }
}

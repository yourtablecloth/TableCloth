// https://github.com/AngryCarrot789/WPFDarkTheme

using System;
using System.Windows;

namespace Spork.Themes
{
    internal static class ThemesController
    {
        private static ThemeTypes? _currentTheme = null;
        private static ResourceDictionary _currentThemeDictionary = null;

        public static ThemeTypes? CurrentTheme
        {
            get => _currentTheme;
            set
            {
                string themeName;
                switch (value)
                {
                    case ThemeTypes.Dark: themeName = nameof(DarkTheme); break;
                    case ThemeTypes.Light: themeName = nameof(LightTheme); break;
                    case ThemeTypes.ColourfulDark: themeName = nameof(ColourfulDarkTheme); break;
                    case ThemeTypes.ColourfulLight: themeName = nameof(ColourfulLightTheme); break;
                    default: return;
                }

                try
                {
                    if (_currentThemeDictionary != null)
                        Application.Current.Resources.MergedDictionaries.Remove(_currentThemeDictionary);

                    // 어셈블리가 진입점(Spork.exe)이 아니라 Spork.App 라이브러리에 있으므로
                    // pack URI에 `;component/` 어셈블리 한정자를 명시. 누락 시 WPF가 진입점 어셈블리에서
                    // 리소스를 찾고 실패하여 catch {} 블록에 묻혀 테마 자체가 미적용된다.
                    var uri = new Uri($"/Spork.App;component/Themes/{themeName}.xaml", UriKind.Relative);
                    _currentThemeDictionary = new ResourceDictionary() { Source = uri };
                    Application.Current.Resources.MergedDictionaries.Add(_currentThemeDictionary);
                }
                catch { }
            }
        }
    }
}

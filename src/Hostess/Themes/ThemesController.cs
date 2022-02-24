// https://github.com/AngryCarrot789/WPFDarkTheme

using System;
using System.Windows;

namespace Hostess.Themes
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

                    var uri = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);
                    _currentThemeDictionary = new ResourceDictionary() { Source = uri };
                    Application.Current.Resources.MergedDictionaries.Add(_currentThemeDictionary);
                }
                catch { }
            }
        }
    }
}

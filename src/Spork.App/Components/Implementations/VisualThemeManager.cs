using Microsoft.Win32;
using Spork.Themes;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TableCloth;

namespace Spork.Components.Implementations
{
    public sealed class VisualThemeManager : IVisualThemeManager
    {
        // ViewModel이 WPF Window/Application.Current를 참조하지 않도록, 내부에서 메인 창을 해석해 적용한다.
        public void ApplyAutoThemeChange()
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow != null)
                ApplyAutoThemeChange(mainWindow);
        }

        public void ApplyAutoThemeChange(Window targetWindow)
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(targetWindow).Handle);
            source.AddHook(WndProc);

            var appliedLightTheme = this.IsLightThemeApplied();
            if (appliedLightTheme.HasValue)
            {
                if (appliedLightTheme.Value)
                    ThemesController.CurrentTheme = ThemeTypes.ColourfulLight;
                else
                    ThemesController.CurrentTheme = ThemeTypes.ColourfulDark;
            }
        }

        private bool? IsLightThemeApplied()
        {
            // https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
            using (var personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            {
                if (personalizeKey != null)
                {
                    var appsUseLightThemeValueName = personalizeKey.GetValueNames().FirstOrDefault(x => string.Equals("AppsUseLightTheme", x, StringComparison.OrdinalIgnoreCase));

                    if (appsUseLightThemeValueName == null)
                        return null;

                    if (personalizeKey.GetValueKind(appsUseLightThemeValueName) == RegistryValueKind.DWord)
                    {
                        return GetValue<int>(personalizeKey, appsUseLightThemeValueName, 1) > 0;
                    }
                }
            }

            return null;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_SETTINGCHANGE)
            {
                var data = Marshal.PtrToStringAuto(lParam);
                if (string.Equals(data, "ImmersiveColorSet", StringComparison.Ordinal))
                {
                    var appliedLightTheme = IsLightThemeApplied();
                    if (appliedLightTheme.HasValue)
                    {
                        if (appliedLightTheme.Value)
                            ThemesController.CurrentTheme = ThemeTypes.ColourfulLight;
                        else
                            ThemesController.CurrentTheme = ThemeTypes.ColourfulDark;
                        handled = true;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private TValue GetValue<TValue>(RegistryKey registryKey, string name,
            TValue defaultValue = default, RegistryValueOptions options = default)
            where TValue : struct
        {
            var value = registryKey.GetValue(name, defaultValue, options) as TValue?;
            return value.HasValue ? value.Value : defaultValue;
        }
    }
}

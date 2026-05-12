using Microsoft.Win32;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using TableCloth.Resources;
using TableCloth.Themes;

namespace TableCloth.Bootstrap.Dialogs;

public partial class LicenseWindow : Window
{
    public LicenseWindow()
    {
        InitializeComponent();
        Loaded += Window_Loaded;

        // Set UI strings from resources
        InstructionLabel.Content = UIStringResources.License_Instruction;
        AgreeButton.Content = UIStringResources.License_AgreeButton;
        DeclineButton.Content = UIStringResources.License_DeclineButton;
        LicenseContentTextBox.Text = UIStringResources.License_Content;
    }

    public bool LicenseAccepted { get; private set; }

    private void AgreeButton_Click(object sender, RoutedEventArgs e)
    {
        LicenseAccepted = true;
        DialogResult = true;
        Close();
    }

    private void DeclineButton_Click(object sender, RoutedEventArgs e)
    {
        LicenseAccepted = false;
        DialogResult = false;
        Close();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source.AddHook(WndProc);

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var appliedLightTheme = IsLightThemeApplied();
        if (appliedLightTheme.HasValue)
        {
            string themeName = appliedLightTheme.Value ? "ColourfulLightTheme" : "ColourfulDarkTheme";
            var uri = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);
            var themeDict = new ResourceDictionary() { Source = uri };
            this.Resources.MergedDictionaries.Clear();
            this.Resources.MergedDictionaries.Add(themeDict);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x001A) // WM_SETTINGCHANGE
        {
            var data = Marshal.PtrToStringAuto(lParam);
            if (string.Equals(data, "ImmersiveColorSet", StringComparison.Ordinal))
            {
                ApplyTheme();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private static bool? IsLightThemeApplied()
    {
        using var personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);

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

        return null;
    }

    private static TValue GetValue<TValue>(RegistryKey registryKey, string name,
        TValue defaultValue = default, RegistryValueOptions options = default)
        where TValue : struct
    {
        var value = registryKey.GetValue(name, defaultValue, options) as TValue?;
        return value ?? defaultValue;
    }
}

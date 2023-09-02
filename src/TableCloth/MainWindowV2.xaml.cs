﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Pages;
using TableCloth.Themes;
using TableCloth.ViewModels;

namespace TableCloth
{
    /// <summary>
    /// Interaction logic for MainWindowV2.xaml
    /// </summary>
    public partial class MainWindowV2 : Window
    {
        public MainWindowV2()
        {
            InitializeComponent();
        }

        public MainWindowV2ViewModel ViewModel
            => (MainWindowV2ViewModel)DataContext;

        // https://stackoverflow.com/questions/2135113/how-do-you-do-transition-effects-using-the-frame-control-in-wpf
        private bool _allowDirectNavigation = true;
        private NavigatingCancelEventArgs _navArgs = null;
        private double _oldValue = 0d;

        private readonly Duration _duration = new Duration(TimeSpan.FromSeconds(0.25));
        private readonly DependencyProperty _targetProperty = OpacityProperty;

        private static bool? IsLightThemeApplied()
        {
            // https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
            using (var personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            {
                if (personalizeKey != null)
                {
                    if (personalizeKey.GetValueKind("AppsUseLightTheme") == RegistryValueKind.DWord)
                    {
                        return (int)personalizeKey.GetValue("AppsUseLightTheme", 1) > 0;
                    }
                }
            }

            return null;
        }

        private static void OpenExplorer(string targetDirectoryPath)
        {
            if (!Directory.Exists(targetDirectoryPath))
                return;

            var psi = new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                targetDirectoryPath)
            {
                UseShellExecute = false,
            };

            Process.Start(psi);
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

        private void PageFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (Content == null || _allowDirectNavigation)
            {
                _allowDirectNavigation = false;
                return;
            }

            e.Cancel = true;
            _navArgs = e;

            var animation0 = new DoubleAnimation();
            animation0.From = _oldValue = PageFrame.Opacity;
            animation0.To = 0;
            animation0.Duration = _duration;
            animation0.Completed += SlideCompleted;

            PageFrame.BeginAnimation(_targetProperty, animation0);
        }

        private void SlideCompleted(object sender, EventArgs e)
        {
            _allowDirectNavigation = true;
            switch (_navArgs.NavigationMode)
            {
                case NavigationMode.New:
                    if (_navArgs.Uri == null)
                        PageFrame.Navigate(_navArgs.Content, _navArgs.ExtraData);
                    else
                        PageFrame.Navigate(_navArgs.Uri, _navArgs.ExtraData);
                    break;
                case NavigationMode.Back:
                    PageFrame.GoBack();
                    break;
                case NavigationMode.Forward:
                    PageFrame.GoForward();
                    break;
                case NavigationMode.Refresh:
                    PageFrame.Refresh();
                    break;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                (ThreadStart)delegate ()
                {
                    var animation0 = new DoubleAnimation();
                    animation0.From = 0;
                    animation0.To = _oldValue;
                    animation0.Duration = _duration;

                    PageFrame.BeginAnimation(_targetProperty, animation0);
                });
        }

        private void PageFrame_LoadCompleted(object sender, NavigationEventArgs e)
        {
            switch (e.Content)
            {
                case IPageArgument<CatalogInternetService> target:
                    target.Arguments = e.ExtraData as IEnumerable<CatalogInternetService>;
                    break;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);

            var appliedLightTheme = IsLightThemeApplied();
            if (appliedLightTheme.HasValue)
            {
                if (appliedLightTheme.Value)
                    ThemesController.CurrentTheme = ThemeTypes.ColourfulLight;
                else
                    ThemesController.CurrentTheme = ThemeTypes.ColourfulDark;
            }

            var services = ViewModel.Services;
            var directoryPath = ViewModel.SharedLocations.GetImageDirectoryPath();

            await ViewModel.ResourceResolver.LoadSiteImages(
                App.Current.Services.GetService<IHttpClientFactory>(),
                services, directoryPath).ConfigureAwait(false);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (var eachDirectory in ViewModel.TemporaryDirectories)
            {
                if (!string.IsNullOrWhiteSpace(ViewModel.CurrentDirectory))
                {
                    if (string.Equals(Path.GetFullPath(eachDirectory), Path.GetFullPath(ViewModel.CurrentDirectory), StringComparison.OrdinalIgnoreCase))
                    {
                        if (ViewModel.SandboxLauncher.IsSandboxRunning())
                        {
                            OpenExplorer(eachDirectory);
                            continue;
                        }
                    }
                }

                if (!Directory.Exists(eachDirectory))
                    continue;

                try { Directory.Delete(eachDirectory, true); }
                catch { OpenExplorer(eachDirectory); }
            }

            if (ViewModel.AppRestartManager.ReserveRestart)
                ViewModel.AppRestartManager.RestartNow();
        }
    }
}
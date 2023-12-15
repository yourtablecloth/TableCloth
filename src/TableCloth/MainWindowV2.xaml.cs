using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Pages;
using TableCloth.Resources;
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

        private void PageFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (Content == null || _allowDirectNavigation)
            {
                _allowDirectNavigation = false;
                return;
            }

            e.Cancel = true;
            _navArgs = e;

            var animation = new DoubleAnimation();
            animation.From = _oldValue = PageFrame.Opacity;
            animation.To = 0;
            animation.Duration = _duration;
            animation.Completed += DoubleAnimation_SlideCompleted;

            PageFrame.BeginAnimation(_targetProperty, animation);
        }

        private void DoubleAnimation_SlideCompleted(object sender, EventArgs e)
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
                case IPageArgument<CatalogPageArgumentModel> target:
                    target.Arguments = e.ExtraData as CatalogPageArgumentModel;
                    break;

                case IPageArgument<DetailPageArgumentModel> target:
                    target.Arguments = e.ExtraData as DetailPageArgumentModel;
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.VisualThemeManager.ApplyAutoThemeChange(this);

            var args = App.Current.Arguments.ToArray();
            var hasArgs = args.Count() > 0;

            if (hasArgs)
            {
                var parsedArg = ViewModel.CommandLineParser.Parse(args);

                if (parsedArg.SelectedService == null)
                    return;

                PageFrame.Navigate(
                    new Uri("Pages/DetailPage.xaml", UriKind.Relative),
                    parsedArg);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ViewModel.SandboxCleanupManager.TryCleanup();

            if (ViewModel.AppRestartManager.ReserveRestart)
                ViewModel.AppRestartManager.RestartNow();
        }
    }
}

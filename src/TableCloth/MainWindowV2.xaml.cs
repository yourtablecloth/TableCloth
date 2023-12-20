using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Threading;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth
{
    /// <summary>
    /// Interaction logic for MainWindowV2.xaml
    /// </summary>
    public partial class MainWindowV2 : Window
    {
        public MainWindowV2(MainWindowV2ViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public MainWindowV2ViewModel ViewModel
            => (MainWindowV2ViewModel)DataContext;

        // https://stackoverflow.com/questions/2135113/how-do-you-do-transition-effects-using-the-frame-control-in-wpf
        private bool _allowDirectNavigation = true;
        private NavigatingCancelEventArgs? _navArgs = null;
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

        private void DoubleAnimation_SlideCompleted(object? sender, EventArgs e)
        {
            var navService = ViewModel.NavigationService;
            _allowDirectNavigation = true;

            if (_navArgs != null)
            {
                switch (_navArgs.NavigationMode)
                {
                    case NavigationMode.New:
                        if (_navArgs.Uri.Equals(new Uri("Pages/CatalogPage.xaml", UriKind.Relative)))
                            navService.NavigateTo<CatalogPage>(_navArgs.ExtraData);
                        else if (_navArgs.Uri.Equals(new Uri("Pages/DetailPage.xaml", UriKind.Relative)))
                            navService.NavigateTo<DetailPage>(_navArgs.ExtraData);
                        break;
                    case NavigationMode.Back:
                        navService.GoBack();
                        break;
                    case NavigationMode.Forward:
                        navService.GoForward();
                        break;
                    case NavigationMode.Refresh:
                        navService.Refresh();
                        break;
                }
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.NavigationService.Initialize(this.PageFrame);
            ViewModel.VisualThemeManager.ApplyAutoThemeChange(this);

            var args = App.Current.Arguments.ToArray();
            var hasArgs = args.Count() > 0;

            if (hasArgs)
            {
                var parsedArg = ViewModel.CommandLineParser.Parse(args);

                if (parsedArg.SelectedService == null)
                    return;

                ViewModel.NavigationService.NavigateTo<DetailPageViewModel>(parsedArg);
            }
            else
                ViewModel.NavigationService.NavigateTo<CatalogPageViewModel>(null);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ViewModel.SandboxCleanupManager.TryCleanup();

            if (ViewModel.AppRestartManager.ReserveRestart)
                ViewModel.AppRestartManager.RestartNow();
        }
    }
}

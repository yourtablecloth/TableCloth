using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace Spork
{
    public partial class MainWindow : Window
    {
        public MainWindow(
            MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.WindowLoaded += ViewModel_WindowLoaded;
            viewModel.CloseRequested += ViewModel_CloseRequested;
        }

        public MainWindowViewModel ViewModel
            => (MainWindowViewModel)DataContext;

        private void ViewModel_WindowLoaded(object sender, EventArgs e)
        {
            // XAML에서 정의된 기본 Width/Height와 WindowStartupLocation=CenterScreen을 그대로 사용한다.
            // (이전에는 stetps 전용 좁은 패널이라 화면 우측 상단에 고정 배치했지만,
            //  카탈로그 브라우징 모드를 위해 더 큰 창을 중앙에 띄운다.)
        }

        private void ViewModel_CloseRequested(object sender, EventArgs e)
        {
            Close();
        }

        private void SponsorBanner_MouseLeftButtonUp(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://yourtablecloth.app/#sponsor",
                    UseShellExecute = true,
                });
            }
            catch { }
        }
    }
}

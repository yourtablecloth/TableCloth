using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using TableCloth.Models.Catalog;

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

        /// <summary>
        /// 사이트 카탈로그 항목 클릭 핸들러. WPF의 ListView는 내부적으로 마우스 이벤트를 흡수하여
        /// 자식 Grid의 InputBindings(MouseAction=LeftClick)가 안정적으로 발화되지 않는 경우가 있다.
        /// 코드비하인드에서 직접 처리하여 단일 클릭만으로 즉시 실행되도록 한다.
        /// 즐겨찾기 별(ToggleButton)을 누른 경우에는 ToggleButton이 먼저 e.Handled = true로 표시하므로
        /// 이 핸들러는 발화되지 않는다.
        /// </summary>
        private void CatalogItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
                return;
            if (sender is not FrameworkElement element || element.DataContext is not CatalogInternetService service)
                return;

            ViewModel.SelectedCatalogService = service;
            if (ViewModel.CatalogItemActivateCommand.CanExecute(null))
                ViewModel.CatalogItemActivateCommand.Execute(null);
        }
    }
}

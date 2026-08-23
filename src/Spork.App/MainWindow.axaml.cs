using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Spork.ViewModels;
using System;
using System.Diagnostics;
using TableCloth.Models.Catalog;

namespace Spork
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        public MainWindow(
            MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.WindowLoaded += ViewModel_WindowLoaded;
            viewModel.CloseRequested += ViewModel_CloseRequested;
            Loaded += OnLoaded;
        }

        public MainWindowViewModel ViewModel
            => (MainWindowViewModel)DataContext!;

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // 이슈 #296: WPF Interaction.Triggers(Loaded) → 코드비하인드 Loaded 훅.
            if (ViewModel.MainWindowLoadedCommand.CanExecute(null))
                ViewModel.MainWindowLoadedCommand.Execute(null);
        }

        private void ViewModel_WindowLoaded(object? sender, EventArgs e)
        {
            // XAML에서 정의된 기본 Width/Height와 WindowStartupLocation=CenterScreen을 그대로 사용한다.
        }

        private void ViewModel_CloseRequested(object? sender, EventArgs e)
            => Close();

        private void SponsorBanner_Click(object? sender, RoutedEventArgs e)
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
        /// 카탈로그 카드 클릭 핸들러(이슈 #296). 즐겨찾기 별/설치 배지(Button)를 누른 경우 그 컨트롤이
        /// PointerReleased 를 Handled 로 표시하므로 이 핸들러는 발화하지 않는다(WPF 동작과 동일).
        /// </summary>
        private void CatalogItem_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (e.Handled)
                return;
            if (sender is not Control element || element.DataContext is not CatalogInternetService service)
                return;

            if (ViewModel.ActivateServiceCommand.CanExecute(service))
                ViewModel.ActivateServiceCommand.Execute(service);
        }
    }
}

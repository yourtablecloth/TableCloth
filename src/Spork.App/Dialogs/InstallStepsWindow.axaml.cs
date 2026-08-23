using Avalonia.Controls;
using Avalonia.Interactivity;
using Spork.ViewModels;
using TableCloth.Events;

namespace Spork.Dialogs
{
    public partial class InstallStepsWindow : Window
    {
        public InstallStepsWindow() => InitializeComponent();

        public InstallStepsWindow(InstallStepsWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += ViewModel_CloseRequested;
            Loaded += OnLoaded;
        }

        public InstallStepsWindowViewModel ViewModel
            => (InstallStepsWindowViewModel)DataContext!;

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (ViewModel.InstallStepsWindowLoadedCommand.CanExecute(null))
                ViewModel.InstallStepsWindowLoadedCommand.Execute(null);
        }

        private void ViewModel_CloseRequested(object? sender, DialogRequestEventArgs e)
            => Close(e.DialogResult);

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            // 사용자가 X로 닫거나, 코드가 Close()를 호출하는 모든 경로에서 호출된다.
            // 진행 중인 설치 단계가 있다면 즉시 취소 신호를 보내고, 닫기 자체는 막지 않는다.
            ViewModel?.CancelInstall();
            base.OnClosing(e);
        }
    }
}

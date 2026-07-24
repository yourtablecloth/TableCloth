using Avalonia.Controls;
using Spork.ViewModels;
using TableCloth.Events;

namespace Spork.Dialogs
{
    /// <summary>
    /// AhnLab Safe Transaction의 "원격 접속 차단" 해제를 안내하는 전용 창(이슈 #275).
    /// </summary>
    public partial class AhnLabSafeTxGuideWindow : Window
    {
        public AhnLabSafeTxGuideWindow() => InitializeComponent();

        public AhnLabSafeTxGuideWindow(
            AhnLabSafeTxGuideWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += ViewModel_CloseRequested;
        }

        public AhnLabSafeTxGuideWindowViewModel ViewModel
            => (AhnLabSafeTxGuideWindowViewModel)DataContext!;

        private void ViewModel_CloseRequested(object? sender, DialogRequestEventArgs e)
            => Close(e.DialogResult);
    }
}

using Avalonia.Controls;
using Spork.ViewModels;
using TableCloth.Events;

namespace Spork.Dialogs
{
    public partial class SandboxGuidanceWindow : Window
    {
        public SandboxGuidanceWindow() => InitializeComponent();

        public SandboxGuidanceWindow(
            SandboxGuidanceWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += ViewModel_CloseRequested;
        }

        public SandboxGuidanceWindowViewModel ViewModel
            => (SandboxGuidanceWindowViewModel)DataContext!;

        private void ViewModel_CloseRequested(object? sender, DialogRequestEventArgs e)
            => Close(e.DialogResult);
    }
}

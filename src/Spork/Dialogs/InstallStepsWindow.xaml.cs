using Spork.ViewModels;
using System.Windows;
using TableCloth.Events;

namespace Spork.Dialogs
{
    public partial class InstallStepsWindow : Window
    {
        public InstallStepsWindow(InstallStepsWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += ViewModel_CloseRequested;
        }

        public InstallStepsWindowViewModel ViewModel
            => (InstallStepsWindowViewModel)DataContext;

        private void ViewModel_CloseRequested(object sender, DialogRequestEventArgs e)
        {
            DialogResult = e.DialogResult;
            Close();
        }
    }
}

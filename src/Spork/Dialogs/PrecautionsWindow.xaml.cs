using Spork.ViewModels;
using System.Windows;
using TableCloth.Events;

namespace Spork.Dialogs
{
    public partial class PrecautionsWindow : Window
    {
        public PrecautionsWindow(
            PrecautionsWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseRequested += ViewModel_CloseRequested;
        }

        private void ViewModel_CloseRequested(object sender, DialogRequestEventArgs e)
        {
            DialogResult = e.DialogResult;
            Close();
        }
    }
}

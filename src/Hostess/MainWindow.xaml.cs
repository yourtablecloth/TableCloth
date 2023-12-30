using Hostess.ViewModels;
using System.Windows;
using TableCloth.Events;

namespace Hostess
{
    public partial class MainWindow : Window
    {
        public MainWindow(
            MainWindowViewModel viewModel)
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

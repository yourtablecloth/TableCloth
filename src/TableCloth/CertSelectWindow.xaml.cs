using System.Windows;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth
{
    public partial class CertSelectWindow : Window
    {
        public CertSelectWindow(
            CertSelectWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.OnRequestClose += ViewModel_OnRequestClose;
        }

        private void ViewModel_OnRequestClose(object? sender, DialogRequestEventArgs e)
        {
            this.DialogResult = e.DialogResult;
            this.Close();
        }

        public CertSelectWindowViewModel ViewModel
            => (CertSelectWindowViewModel)DataContext;
    }
}

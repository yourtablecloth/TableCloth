using Spork.ViewModels;
using System.Windows;
using TableCloth.Events;

namespace Spork.Dialogs
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow(AboutWindowViewModel aboutWindowViewModel)
        {
            InitializeComponent();
            DataContext = aboutWindowViewModel;
            aboutWindowViewModel.CloseRequested += AboutWindowViewModel_CloseRequested;
        }

        private void AboutWindowViewModel_CloseRequested(object sender, DialogRequestEventArgs e)
        {
            DialogResult = e.DialogResult;
            Close();
        }
    }
}

using Hostess.ViewModels;
using System;
using System.Windows;

namespace Hostess
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

        private void ViewModel_WindowLoaded(object sender, EventArgs e)
        {
            Width = MinWidth;
            Height = SystemParameters.PrimaryScreenHeight * 0.5;
            Top = (SystemParameters.PrimaryScreenHeight / 2) - (Height / 2);
            Left = SystemParameters.PrimaryScreenWidth - Width;
        }

        private void ViewModel_CloseRequested(object sender, EventArgs e)
        {
            Close();
        }
    }
}

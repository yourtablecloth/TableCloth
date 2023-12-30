using Hostess.Components;
using Hostess.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using TableCloth.Events;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Hostess.Dialogs
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

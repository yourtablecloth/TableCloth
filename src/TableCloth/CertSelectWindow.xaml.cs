using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TableCloth.Implementations.WPF
{
    /// <summary>
    /// Interaction logic for CertSelectWindow.xaml
    /// </summary>
    public partial class CertSelectWindow : Window
    {
        public CertSelectWindow()
        {
            InitializeComponent();
        }

        private void RefreshCertPairsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CertSelectWindowViewModel vm)
                vm.RefreshCertPairs();
        }

        private void OpenCertPairManuallyButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = DataContext is CertSelectWindowViewModel vm && vm.SelectedCertPair != null;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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
using TableCloth.Models.Configuration;
using TableCloth.Resources;
using TableCloth.ViewModels;

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
            if (DataContext is not CertSelectWindowViewModel vm)
                return;

            var ofd = new OpenFileDialog()
            {
                AddExtension = true,
                CheckFileExists = true,
                CheckPathExists = true,
                DereferenceLinks = true,
                Filter = (string)Application.Current.Resources["CertSelectWindow_FileOpenDialog_FilterText"],
                FilterIndex = 0,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Multiselect = true,
                ReadOnlyChecked = true,
                ShowReadOnly = false,
                Title = (string)Application.Current.Resources["CertSelectWindow_FileOpenDialog_Text"],
                ValidateNames = true,
            };

            // To Do: NPKI 폴더 외에 이동식 드라이브, 사용자 인증서 폴더 등을 미리 추가할 수 있을지 검토 필요
            var npkiPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NPKI");

            if (Directory.Exists(npkiPath))
            {
                ofd.CustomPlaces = new FileDialogCustomPlace[]
                {
                new FileDialogCustomPlace(npkiPath),
                };
            }

            var response = ofd.ShowDialog();

            if (response.HasValue && response.Value)
            {
                vm.SelectedCertPair = vm.CertPairScanner.CreateX509CertPair(
                    ofd.FileNames.Where(x => string.Equals(".der", System.IO.Path.GetExtension(x), StringComparison.OrdinalIgnoreCase)).First(),
                    ofd.FileNames.Where(x => string.Equals(".key", System.IO.Path.GetExtension(x), StringComparison.OrdinalIgnoreCase)).First()
                );
                DialogResult = true;
                Close();
                return;
            }
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

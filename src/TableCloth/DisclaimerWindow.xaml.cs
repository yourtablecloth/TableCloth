using System.Windows;
using System.Windows.Interop;

namespace TableCloth
{
    public partial class DisclaimerWindow : Window
    {
        public DisclaimerWindow()
        {
            InitializeComponent();
        }

        private void AgreeDisclaimer_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            NativeMethods.SetWindowLongW(
                hwnd,
                NativeMethods.GWL_STYLE,
                NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_STYLE) & ~NativeMethods.WS_SYSMENU);
        }
    }
}

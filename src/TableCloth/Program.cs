using System;
using System.Threading;
using System.Windows.Forms;
using TableCloth.Resources;

namespace TableCloth
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            _ = Application.OleRequired();
            Application.EnableVisualStyles();
            _ = Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.SetCompatibleTextRenderingDefault(false);

            var context = CreateAppContext();

            if (context == null)
                return;

            Application.Run(context);
        }

        public static ApplicationContext CreateAppContext()
        {
            _ = new Mutex(true, typeof(Program).FullName, out var isFirstInstance);

            if (!isFirstInstance)
            {
                _ = MessageBox.Show(StringResources.Error_Already_TableCloth_Running, StringResources.TitleText_Warning,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                return null;
            }

            var form = ScreenBuilder.CreateMainForm();
            var appContext = new ApplicationContext(form);
            return appContext;
        }
    }
}

using System;
using System.Windows.Forms;

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
            Application.Run(context);
        }

        public static ApplicationContext CreateAppContext()
        {
            var form = ScreenBuilder.CreateMainForm();
            var appContext = new ApplicationContext(form);
            return appContext;
        }
    }
}

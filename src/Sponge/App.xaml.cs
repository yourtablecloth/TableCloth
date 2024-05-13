using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;

namespace Sponge
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (!Helpers.SandboxAccountNames.Contains(Environment.UserName, StringComparer.OrdinalIgnoreCase))
            {
                MessageBox.Show("이 프로그램은 Windows Sandbox 환경에서만 실행하도록 설계되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);

#if !DEBUG
                Shutdown();
#endif // !DEBUG
            }
        }
    }
}

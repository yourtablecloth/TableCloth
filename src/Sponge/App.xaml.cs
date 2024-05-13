using System.Windows;
using TableCloth;
using TableCloth.Resources;

namespace Sponge
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (!Helpers.IsUnderWindowsSandboxSession())
            {
                MessageBox.Show(ErrorStrings.Error_Sponge_NotEligible, UIStringResources.TitleText_Info, MessageBoxButton.OK, MessageBoxImage.Warning);

#if !DEBUG
                Shutdown();
#endif // !DEBUG
            }
        }
    }
}

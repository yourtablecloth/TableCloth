using System.Diagnostics;
using System.Windows;
using TableCloth.Resources;

namespace TableCloth.Components
{
    public sealed class AppRestartManager
    {
        public AppRestartManager(AppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly AppMessageBox _appMessageBox;

        public bool ReserveRestart { get; set; }

        public bool AskRestart()
            => _appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK);

        public void RestartNow()
        {
            Process.Start(
                Process.GetCurrentProcess().MainModule.FileName,
                App.Current.Arguments);
            Application.Current.Shutdown();
        }
    }
}

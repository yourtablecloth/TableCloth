using System.Diagnostics;
using System.Windows;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class AppRestartManager(
    Application application,
    IAppMessageBox appMessageBox,
    ISharedLocations sharedLocations) : IAppRestartManager
{
    private bool _restartReserved;

    public bool AskRestart()
        => appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK);

    public void RestartNow()
    {
        Process.Start(sharedLocations.ExecutableFilePath, Helpers.GetCommandLineArguments());
        application.Shutdown();
    }

    public void ReserveRestart()
        => _restartReserved = true;

    public bool IsRestartReserved()
        => _restartReserved;
}

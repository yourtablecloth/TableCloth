using System.Diagnostics;
using System.Windows;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class AppRestartManager : IAppRestartManager
{
    public AppRestartManager(
        Application application,
        IAppMessageBox appMessageBox,
        ISharedLocations sharedLocations)
    {
        _application = application;
        _appMessageBox = appMessageBox;
        _sharedLocations = sharedLocations;
    }

    private readonly Application _application;
    private readonly IAppMessageBox _appMessageBox;
    private readonly ISharedLocations _sharedLocations;

    private bool _restartReserved;

    public bool AskRestart()
        => _appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK);

    public void RestartNow()
    {
        Process.Start(_sharedLocations.ExecutableFilePath, Helpers.GetCommandLineArguments());
        _application.Shutdown();
    }

    public void ReserveRestart()
        => _restartReserved = true;

    public bool IsRestartReserved()
        => _restartReserved;
}

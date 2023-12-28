using System.Diagnostics;
using System.Windows;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class AppRestartManager
{
    public AppRestartManager(
        AppMessageBox appMessageBox,
        SharedLocations sharedLocations)
    {
        _appMessageBox = appMessageBox;
        _sharedLocations = sharedLocations;
    }

    private readonly AppMessageBox _appMessageBox;
    private readonly SharedLocations _sharedLocations;

    public bool ReserveRestart { get; set; }

    public bool AskRestart()
        => _appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK);

    public void RestartNow()
    {
        Process.Start(_sharedLocations.ExecutableFilePath, Helpers.GetCommandLineArguments());
        Application.Current.Shutdown();
    }
}

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class AppRestartManager
{
    public AppRestartManager(
        Application application,
        AppMessageBox appMessageBox,
        SharedLocations sharedLocations)
    {
        _application = application;
        _appMessageBox = appMessageBox;
        _sharedLocations = sharedLocations;
    }

    private readonly Application _application;
    private readonly AppMessageBox _appMessageBox;
    private readonly SharedLocations _sharedLocations;

    public bool ReserveRestart { get; set; }

    public bool AskRestart()
        => _appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK);

    public void RestartNow([CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
    {
        _appMessageBox.DisplayInfo($"{file} - {member} - {line}");
        Process.Start(_sharedLocations.ExecutableFilePath, Helpers.GetCommandLineArguments());
        _application.Shutdown();
    }
}

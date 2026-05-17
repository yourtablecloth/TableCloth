using System.Diagnostics;
using System.Windows;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class AppRestartManager(
    IApplicationService applicationService,
    IAppMessageBox appMessageBox,
    ISharedLocations sharedLocations) : IAppRestartManager
{
    private bool _restartReserved;

    public bool AskRestart()
        => appMessageBox.DisplayInfo(AskStrings.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK);

    public void RestartNow()
    {
        Process.Start(sharedLocations.ExecutableFilePath, Helpers.GetCommandLineArguments());
        applicationService.Shutdown(CodeResources.ExitCode_Succeed);
    }

    public void ReserveRestart()
        => _restartReserved = true;

    public bool IsRestartReserved()
        => _restartReserved;
}

using System.Diagnostics;
using TableCloth.Models;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class AppRestartManager(
    IApplicationService applicationService,
    IAppMessageBox appMessageBox,
    ISharedLocations sharedLocations) : IAppRestartManager
{
    private bool _restartReserved;

    public bool AskRestart()
        => appMessageBox.DisplayInfo(AskStrings.Ask_RestartRequired, AppMessageBoxButton.OKCancel).Equals(AppMessageBoxResult.OK);

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

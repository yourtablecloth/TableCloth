using Spork.Dialogs;
using Spork.ViewModels;
using System.Collections.Generic;

namespace Spork.Components
{
    public interface IAppUserInterface
    {
        AboutWindow CreateAboutWindow();
        MainWindow CreateMainWindow();
        PrecautionsWindow CreatePrecautionsWindow(IEnumerable<string> targetServiceIds = null);
        SiteReportWindow CreateSiteReportWindow();
        InstallStepsWindow CreateInstallStepsWindow(IList<StepItemViewModel> steps, bool dryRun, string targetTitle = null, string targetIconKey = null);
    }
}
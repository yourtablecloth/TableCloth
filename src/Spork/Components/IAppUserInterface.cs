using Spork.Dialogs;
using System.Collections.Generic;

namespace Spork.Components
{
    public interface IAppUserInterface
    {
        AboutWindow CreateAboutWindow();
        MainWindow CreateMainWindow();
        PrecautionsWindow CreatePrecautionsWindow(IEnumerable<string> targetServiceIds = null);
    }
}
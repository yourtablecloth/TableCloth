using Spork.Dialogs;

namespace Spork.Components
{
    public interface IAppUserInterface
    {
        AboutWindow CreateAboutWindow();
        MainWindow CreateMainWindow();
        PrecautionsWindow CreatePrecautionsWindow();
    }
}
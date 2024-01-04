using Hostess.Dialogs;

namespace Hostess.Components
{
    public interface IAppUserInterface
    {
        AboutWindow CreateAboutWindow();
        MainWindow CreateMainWindow();
        PrecautionsWindow CreatePrecautionsWindow();
    }
}
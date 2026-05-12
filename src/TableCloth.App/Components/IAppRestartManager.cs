namespace TableCloth.Components;

public interface IAppRestartManager
{
    void ReserveRestart();
    bool IsRestartReserved();
    bool AskRestart();
    void RestartNow();
}
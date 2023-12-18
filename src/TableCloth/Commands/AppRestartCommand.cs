using TableCloth.Components;

namespace TableCloth.Commands
{
    public sealed class AppRestartCommand : CommandBase
    {
        public AppRestartCommand(
            AppRestartManager appRestartManager)
        {
            this.appRestartManager = appRestartManager;
        }

        private readonly AppRestartManager appRestartManager;

        public override void Execute(object parameter)
        {
            this.appRestartManager.RestartNow();
        }
    }
}

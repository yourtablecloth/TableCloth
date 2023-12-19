using TableCloth.Components;

namespace TableCloth.Commands
{
    public sealed class AboutThisAppCommand : CommandBase
    {
        public AboutThisAppCommand(AppUserInterface appUserInterface)
        {
            this.appUserInterface = appUserInterface;
        }

        private readonly AppUserInterface appUserInterface;

        public override void Execute(object parameter)
        {
            var aboutWindow = this.appUserInterface.CreateWindow<AboutWindow>();
            aboutWindow.ShowDialog();
        }
    }
}

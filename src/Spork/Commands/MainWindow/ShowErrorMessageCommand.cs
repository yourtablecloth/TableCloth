using Spork.Components;

namespace Spork.Commands.MainWindow
{
    public sealed class ShowErrorMessageCommand : CommandBase
    {
        public ShowErrorMessageCommand(
            IAppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly IAppMessageBox _appMessageBox;

        public override void Execute(object parameter)
            => _appMessageBox.DisplayError(parameter as string, true);
    }
}

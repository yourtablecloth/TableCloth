namespace TableCloth.Commands
{
    public sealed class AboutThisAppCommand : BaseCommand
    {
        public override void Execute(object parameter)
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog();
        }
    }
}

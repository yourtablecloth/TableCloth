using System.Diagnostics;
using TableCloth.Resources;

namespace Hostess.Commands.AboutWindow
{
    public sealed class OpenAppHomepageCommand : CommandBase
    {
        public override void Execute(object parameter)
            => Process.Start(new ProcessStartInfo(CommonStrings.AppInfoUrl) { UseShellExecute = true });
    }
}

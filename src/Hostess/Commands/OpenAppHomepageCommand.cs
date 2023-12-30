using System.Diagnostics;
using TableCloth.Resources;

namespace Hostess.Commands
{
    public sealed class OpenAppHomepageCommand : CommandBase
    {
        public override void Execute(object parameter)
            => Process.Start(new ProcessStartInfo(StringResources.AppInfoUrl) { UseShellExecute = true });
    }
}

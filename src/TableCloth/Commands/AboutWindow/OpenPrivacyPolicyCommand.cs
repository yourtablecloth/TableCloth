using System.Diagnostics;
using TableCloth.Resources;

namespace TableCloth.Commands.AboutWindow;

public sealed class OpenWebsiteCommand : CommandBase
{
    public override void Execute(object? parameter)
        => Process.Start(new ProcessStartInfo(StringResources.AppInfoUrl) { UseShellExecute = true });
}

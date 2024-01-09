using System.Diagnostics;
using TableCloth.Resources;

namespace TableCloth.Commands;

public sealed class OpenWebsiteCommand : CommandBase
{
    public override void Execute(object? parameter)
        => Process.Start(new ProcessStartInfo(CommonStrings.AppInfoUrl) { UseShellExecute = true });
}

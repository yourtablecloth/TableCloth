using System.Diagnostics;
using TableCloth.Resources;

namespace TableCloth.Commands.AboutWindow;

public sealed class OpenSponsorPageCommand : CommandBase
{
    public override void Execute(object? parameter)
        => Process.Start(new ProcessStartInfo(CommonStrings.SponsorshipUrl) { UseShellExecute = true });
}

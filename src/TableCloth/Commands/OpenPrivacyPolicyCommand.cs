using System.Diagnostics;
using TableCloth.Resources;

namespace TableCloth.Commands;

public sealed class OpenPrivacyPolicyCommand : CommandBase
{
    public override void Execute(object? parameter)
        => Process.Start(new ProcessStartInfo(StringResources.PrivacyPolicyUrl) { UseShellExecute = true });
}

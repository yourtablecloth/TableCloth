using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.ManagedAi;

public interface IManagedAiHostBrowser
{
    Task OpenAsync(Uri target, CancellationToken cancellationToken);
}

public sealed class ManagedAiHostBrowser : IManagedAiHostBrowser
{
    public Task OpenAsync(Uri target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safe = PublicWebUrl.Validate(target.OriginalString);
        try
        {
            Process.Start(new ProcessStartInfo(safe.OriginalString) { UseShellExecute = true })?.Dispose();
            return Task.CompletedTask;
        }
        catch
        {
            throw new ManagedAiException(AiFailureCode.BrowserOpenFailed);
        }
    }
}

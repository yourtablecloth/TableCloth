namespace TableCloth.ManagedAi.OpenAi;

public sealed class OpenAiCodexAuthManager(ManagedAiPaths paths, IManagedRuntimeManager runtimes,
    IManagedAiProfile profile, IJsonlProcessRunner runner) : IProviderAuthentication
{
    public async Task<bool> IsLoggedInAsync(CancellationToken cancellationToken)
    {
        profile.Prepare();
        using var lease = paths.AcquireOperation();
        var runtime = await runtimes.GetActiveAsync(cancellationToken);
        return runtime is not null && await CheckStatusAsync(runtime, cancellationToken);
    }

    internal async Task<bool> CheckStatusAsync(ManagedRuntime runtime, CancellationToken cancellationToken)
    {
        bool chatGpt = false;
        void Check(string line) { if (line.Trim().Equals("Logged in using ChatGPT", StringComparison.OrdinalIgnoreCase)) chatGpt = true; }
        var result = await runner.RunAsync(OpenAiCodexInvocationBuilder.Command(runtime, paths.Profile,
            ["login", "status"], TimeSpan.FromSeconds(20)), Check, Check, cancellationToken);
        return result.ExitCode == 0 && chatGpt;
    }

    public Task LoginAsync(CancellationToken cancellationToken)
        => LoginAsync(AiLoginMethod.DeviceCode, null, cancellationToken);

    public async Task LoginAsync(AiLoginMethod method, IProgress<AiLoginUpdate>? progress, CancellationToken cancellationToken)
    {
        profile.Prepare();
        using var lease = paths.AcquireOperation();
        var runtime = await runtimes.GetActiveAsync(cancellationToken) ?? throw new ManagedAiException(AiFailureCode.RuntimeNotInstalled);
        // Reopening Login must not start another OAuth flow when credentials are already available.
        if (await CheckStatusAsync(runtime, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new(AiLoginStage.Completed));
            return;
        }
        progress?.Report(new(AiLoginStage.Starting));
        var parser = new CodexLoginOutputParser(method, progress);
        string[] arguments = method == AiLoginMethod.DeviceCode ? ["login", "--device-auth"] : ["login"];
        using var pending = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var login = runner.RunAsync(OpenAiCodexInvocationBuilder.Command(runtime, paths.Profile,
            arguments, TimeSpan.FromMinutes(15)), parser.Accept, parser.Accept, pending.Token);
        var authenticated = WaitForAuthenticationAsync(runtime, pending.Token);
        try
        {
            if (await Task.WhenAny(login, authenticated) == authenticated)
            {
                // The browser callback may have saved credentials while login or its inherited pipes remain open.
                // Only a successful ChatGPT status check can finish this wait, never a URL or a success banner.
                await authenticated;
                cancellationToken.ThrowIfCancellationRequested();
                await pending.CancelAsync();
                try { await login; }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            }
            else
            {
                await login;
                await pending.CancelAsync();
                await ObserveAsync(authenticated);
                if (!await CheckStatusAsync(runtime, cancellationToken))
                    throw new ManagedAiException(parser.Failure);
            }
        }
        finally
        {
            await pending.CancelAsync();
            // Keep the operation lease until both child processes have actually stopped.
            await ObserveAsync(Task.WhenAll(login, authenticated));
        }
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new(AiLoginStage.Completed));
    }

    private async Task WaitForAuthenticationAsync(ManagedRuntime runtime, CancellationToken token)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), token);
            try
            {
                // This operation already holds the lease; do not recursively call IsLoggedInAsync.
                if (await CheckStatusAsync(runtime, token)) return;
            }
            catch (ManagedAiException ex) when (ex.Code is AiFailureCode.ProviderFailed or AiFailureCode.ProviderTimeout)
            { /* A transient probe failure does not interrupt an otherwise active login. */ }
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task; }
        catch { /* Only used after cancellation or after the primary outcome has been observed. */ }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        profile.Prepare();
        using var lease = paths.AcquireOperation();
        var runtime = await runtimes.GetActiveAsync(cancellationToken) ?? throw new ManagedAiException(AiFailureCode.RuntimeNotInstalled);
        var result = await runner.RunAsync(OpenAiCodexInvocationBuilder.Command(runtime, paths.Profile,
            ["logout"], TimeSpan.FromSeconds(20)), _ => { }, null, cancellationToken);
        if (result.ExitCode != 0) throw new ManagedAiException(AiFailureCode.ProviderFailed);
    }
}

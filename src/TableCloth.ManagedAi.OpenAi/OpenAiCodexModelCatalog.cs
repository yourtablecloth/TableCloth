namespace TableCloth.ManagedAi.OpenAi;

public sealed class OpenAiCodexModelCatalog(ManagedAiPaths paths, IManagedRuntimeManager runtimes,
    IManagedAiProfile profile, IJsonlProcessRunner runner) : IManagedAiModelCatalog
{
    public async Task<IReadOnlyList<AiModel>> ListAsync(CancellationToken cancellationToken)
    {
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        var runtime = await runtimes.GetActiveAsync(cancellationToken) ?? throw new ManagedAiException(AiFailureCode.RuntimeNotInstalled);
        var directory = paths.Under("runs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var exchange = new CodexModelListExchange();
            var spec = OpenAiCodexInvocationBuilder.Command(runtime, directory,
                ["--strict-config", "app-server", "--listen", "stdio://"], TimeSpan.FromSeconds(30)) with
            {
                Input = CodexModelListExchange.Initialize, Respond = exchange.Accept,
                MaxStdoutBytes = 2 * 1024 * 1024, MaxLineBytes = 1024 * 1024
            };
            var outcome = await runner.RunAsync(spec, _ => { }, null, cancellationToken);
            return exchange.Complete(outcome.ExitCode).Select(OpenAiCodexRateCard.WithCost).ToArray();
        }
        finally { paths.DeleteOwnedDirectory(directory); }
    }
}

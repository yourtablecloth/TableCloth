using System.Diagnostics;
using System.Text;

namespace TableCloth.ManagedAi.OpenAi;

public sealed class OpenAiCodexProvider(ManagedAiPaths paths, IManagedRuntimeManager runtimes,
    OpenAiCodexAuthManager authentication, IManagedAiProfile profile, IJsonlProcessRunner runner,
    ManagedAiOptions options, DiagnosticsSink diagnostics) : IManagedAiProvider, IManagedAiChatProvider
{
    public async Task<AiSearchResponse> SearchAsync(AiSearchRequest request, IProgress<AiProgress>? progress,
        CancellationToken cancellationToken)
    {
        var prompt = OpenAiSearchPromptFactory.Create(request);
        return await RunAsync(prompt, structured: true, (parser, exitCode) => parser.Complete(exitCode, request.MaxResults), progress, cancellationToken);
    }

    public async Task<AiChatResponse> ChatAsync(AiChatRequest request, IProgress<AiProgress>? progress, CancellationToken cancellationToken)
    {
        if (request.Model is not null) AiModel.ValidateId(request.Model);
        return await RunAsync(OpenAiChatPromptFactory.Create(request), structured: false,
            (parser, exitCode) => new AiChatResponse(parser.CompleteText(exitCode), DateTimeOffset.UtcNow, parser.SearchCalls, request.Model), progress, cancellationToken, request.Model);
    }

    private async Task<T> RunAsync<T>(string prompt, bool structured, Func<CodexJsonlParser, int, T> complete,
        IProgress<AiProgress>? progress, CancellationToken cancellationToken, string? model = null)
    {
        profile.Prepare();
        using var lease = paths.AcquireOperation();
        var runtime = await runtimes.GetActiveAsync(cancellationToken) ?? throw new ManagedAiException(AiFailureCode.RuntimeNotInstalled);
        progress?.Report(new("CheckingAuthentication"));
        if (!await authentication.CheckStatusAsync(runtime, cancellationToken))
            throw new ManagedAiException(AiFailureCode.AuthenticationRequired);
        var runId = Guid.NewGuid();
        var runDirectory = paths.Under("runs", runId.ToString("N"));
        Directory.CreateDirectory(runDirectory);
        var parser = new CodexJsonlParser();
        var watch = Stopwatch.StartNew();
        ProcessOutcome? outcome = null;
        AiFailureCode? failure = null;
        bool cancelled = false;
        using var noticeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        async Task NoticeAsync()
        {
            try
            {
                await Task.Delay(options.SoftTimeout, noticeCancellation.Token);
                progress?.Report(new("TakingLonger"));
            }
            catch (OperationCanceledException) { }
        }
        var notice = NoticeAsync();
        try
        {
            var schema = Path.Combine(runDirectory, "search-result.schema.json");
            if (structured)
            {
                await using var source = typeof(OpenAiCodexProvider).Assembly.GetManifestResourceStream(
                    "TableCloth.ManagedAi.OpenAi.Schemas.search-result.schema.json")!;
                await using var destination = new FileStream(schema, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(destination, cancellationToken);
            }
            progress?.Report(new("Thinking"));
            var invocation = structured ? OpenAiCodexInvocationBuilder.Search(runtime, runDirectory, schema, prompt, options)
                : OpenAiCodexInvocationBuilder.Chat(runtime, runDirectory, prompt, options, model);
            outcome = await runner.RunAsync(invocation, line =>
            {
                var before = parser.SearchCalls;
                var stageBefore = parser.Stage;
                parser.Accept(line);
                if (parser.SearchCalls != before || parser.Stage != stageBefore) progress?.Report(new(parser.Stage, parser.SearchCalls));
            }, parser.AcceptDiagnostic, cancellationToken);
            progress?.Report(new("Validating"));
            // Validate the response before writing the content-free outcome log.
            return complete(parser, outcome.ExitCode);
        }
        catch (ManagedAiException ex) { failure = ex.Code; throw; }
        catch (OperationCanceledException) { cancelled = true; throw; }
        catch { failure = AiFailureCode.ProviderFailed; throw new ManagedAiException(failure.Value); }
        finally
        {
            await noticeCancellation.CancelAsync();
            await notice;
            diagnostics.Write(runId, runtime.Coordinate.Version, watch.ElapsedMilliseconds, parser.EventCount,
                parser.SearchCalls, outcome?.StdoutBytes ?? 0, outcome?.StderrBytes ?? 0, failure, cancelled);
            paths.DeleteOwnedDirectory(runDirectory);
        }
    }
}

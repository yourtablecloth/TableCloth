using System.Diagnostics;
using System.Text;

namespace TableCloth.ManagedAi.OpenAi;

public sealed class OpenAiCodexProvider(ManagedAiPaths paths, IManagedRuntimeManager runtimes,
    OpenAiCodexAuthManager authentication, IManagedAiProfile profile, IJsonlProcessRunner runner,
    ManagedAiOptions options, DiagnosticsSink diagnostics, OpenAiCodexSkillManager skills,
    IManagedAiSkillExecution? skillExecution = null, IHttpClientFactory? publicDocumentClients = null) : IManagedAiProvider, IManagedAiChatProvider
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
        // Freeze the caller's list before the first await so the prompt and staged images describe the same turn.
        var attachments = request.Attachments?.ToArray() ?? [];
        AiChatAttachmentPolicy.Validate(attachments);
        return await RunAsync(OpenAiChatPromptFactory.Create(request with { Attachments = attachments }), structured: false,
            (parser, exitCode) => new AiChatResponse(parser.CompleteText(exitCode), DateTimeOffset.UtcNow, parser.SearchCalls, request.Model)
            {
                SkillExecutions = AiSkillExecutionHistory.Snapshot(parser.SkillExecutions),
                SkillExecutionsTruncated = parser.SkillExecutionsTruncated
            }, progress, cancellationToken, request.Model, attachments);
    }

    private async Task<T> RunAsync<T>(string prompt, bool structured, Func<CodexJsonlParser, int, T> complete,
        IProgress<AiProgress>? progress, CancellationToken cancellationToken, string? model = null,
        IReadOnlyList<AiChatAttachment>? attachments = null)
    {
        using var lease = paths.AcquireOperation();
        profile.Prepare();
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
            var imagePaths = new List<string>();
            foreach (var image in (attachments ?? []).Where(attachment => attachment.IsImage))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = paths.Under("runs", runId.ToString("N"), "attachments");
                Directory.CreateDirectory(directory);
                var extension = image.MediaType == "image/png" ? ".png" : ".jpg";
                var file = paths.Under("runs", runId.ToString("N"), "attachments", $"image-{imagePaths.Count + 1:000}{extension}");
                await using (var destination = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await destination.WriteAsync(image.GetContent(), cancellationToken);
                imagePaths.Add(file);
            }
            var availableSkills = await skills.SynchronizeAsync(runtime, runDirectory, cancellationToken);
            using var bridge = !structured && skillExecution is not null
                ? await skillExecution.PrepareAsync(availableSkills, runDirectory, cancellationToken) : null;
            if (bridge is not null)
            {
                // Legacy injected bridges have only tool IDs. Real Windows bridges retain the exact
                // installed skill and entry-point mapping, including multi-entry and dotted IDs.
                var tools = bridge.Tools.Count != 0 ? bridge.Tools : bridge.SkillIds.Select(id =>
                    new AiSkillToolRegistration(id, id, id, "")).ToArray();
                parser.ConfigureSkillTools(tools.Where(tool => bridge.SkillIds.Contains(tool.ToolName, StringComparer.Ordinal)).ToArray());
            }
            var schema = Path.Combine(runDirectory, "search-result.schema.json");
            if (structured)
            {
                await using var source = typeof(OpenAiCodexProvider).Assembly.GetManifestResourceStream(
                    "TableCloth.ManagedAi.OpenAi.Schemas.search-result.schema.json")!;
                await using var destination = new FileStream(schema, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(destination, cancellationToken);
            }
            progress?.Report(new("Thinking"));
            var chatPrompt = prompt;
            if (!structured && publicDocumentClients is not null)
                chatPrompt += "\n\n" + await InstalledSkillPromptContext.CreateAsync(availableSkills, publicDocumentClients, cancellationToken);
            var invocation = structured ? OpenAiCodexInvocationBuilder.Search(runtime, runDirectory, schema, prompt, options)
                : OpenAiCodexInvocationBuilder.Chat(runtime, runDirectory, chatPrompt, options, model, bridge, imagePaths);
            outcome = await runner.RunAsync(invocation, line =>
            {
                var before = parser.SearchCalls;
                var stageBefore = parser.Stage;
                parser.Accept(line);
                if (parser.SearchCalls != before || parser.Stage != stageBefore || parser.LatestSkillExecution is not null)
                    progress?.Report(new(parser.Stage, parser.SearchCalls) { SkillExecution = parser.LatestSkillExecution });
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

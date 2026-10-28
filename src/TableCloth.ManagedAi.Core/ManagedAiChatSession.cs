namespace TableCloth.ManagedAi;

public sealed class ManagedAiChatSession(IManagedAiChatProvider provider, ITableClothBrowser browser)
{
    private readonly List<AiChatMessage> _history = [];
    public IReadOnlyList<AiChatMessage> History => _history.AsReadOnly();
    public void Clear() => _history.Clear();

    public async Task<AiChatResponse> SendAsync(string message, IProgress<AiProgress>? progress, CancellationToken token,
        string? model = null, string? localCertificateReport = null, string? localWindowsSandboxReport = null,
        AiClientContext? clientContext = null, IReadOnlyList<AiChatAttachment>? attachments = null)
    {
        // Each request starts an ephemeral Codex turn. Only bounded in-memory context is sent for follow-ups.
        AiChatAttachmentPolicy.Validate(attachments);
        IReadOnlyList<AiChatAttachment>? snapshot = attachments is { Count: > 0 }
            ? Array.AsReadOnly(attachments.ToArray()) : null;
        AiChatAttachmentPolicy.Validate(snapshot);
        var safeMessage = SensitiveDataSanitizer.Sanitize(message);
        if (string.IsNullOrWhiteSpace(safeMessage) && snapshot is null)
            throw new ManagedAiException(AiFailureCode.InvalidQuery);
        var response = await provider.ChatAsync(new(safeMessage, _history.ToArray(), model, localCertificateReport,
            localWindowsSandboxReport, clientContext, snapshot), progress, token);
        var executions = AiSkillExecutionHistory.Snapshot(response.SkillExecutions);
        response = response with { SkillExecutions = executions };
        // Attachment content is sent only with this request. Follow-ups retain
        // names as context, never file bytes, extracted text, or host paths.
        var historyMessage = safeMessage;
        if (snapshot is not null)
        {
            var placeholders = string.Join("\n", snapshot.Select(attachment =>
                $"[Attachment sent with this request only: {attachment.FileName}]"));
            historyMessage += (string.IsNullOrEmpty(historyMessage) ? "" : "\n\n") + placeholders;
        }
        _history.Add(new(AiChatRole.User, historyMessage));
        _history.Add(new(AiChatRole.Assistant, response.Text)
        {
            SkillExecutions = executions,
            SkillExecutionsTruncated = response.SkillExecutionsTruncated
        });
        while (_history.Count > 12 || _history.Sum(x => x.Text.Length + AiSkillExecutionHistory.Measure(x.SkillExecutions)) > 20000)
            _history.RemoveRange(0, Math.Min(2, _history.Count));
        return response;
    }

    // Called only by a user click; any safe public web link in any displayed message can be opened.
    public Task OpenLinkAsync(Uri link, CancellationToken token)
        => browser.OpenAsync(PublicWebUrl.Validate(link.OriginalString), token);
}

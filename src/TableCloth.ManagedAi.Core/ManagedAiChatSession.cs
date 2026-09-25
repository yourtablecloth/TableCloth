namespace TableCloth.ManagedAi;

public sealed class ManagedAiChatSession(IManagedAiChatProvider provider, ITableClothBrowser browser)
{
    private readonly List<AiChatMessage> _history = [];
    public IReadOnlyList<AiChatMessage> History => _history.AsReadOnly();
    public void Clear() => _history.Clear();

    public async Task<AiChatResponse> SendAsync(string message, IProgress<AiProgress>? progress, CancellationToken token, string? model = null)
    {
        // Each request starts an ephemeral Codex turn. Only bounded in-memory context is sent for follow-ups.
        var response = await provider.ChatAsync(new(message, _history.ToArray(), model), progress, token);
        _history.Add(new(AiChatRole.User, message));
        _history.Add(new(AiChatRole.Assistant, response.Text));
        while (_history.Count > 12 || _history.Sum(x => x.Text.Length) > 20000)
            _history.RemoveRange(0, Math.Min(2, _history.Count));
        return response;
    }

    // Called only by a user click; any safe public web link in any displayed message can be opened.
    public Task OpenLinkAsync(Uri link, CancellationToken token)
        => browser.OpenAsync(PublicWebUrl.Validate(link.OriginalString), token);
}

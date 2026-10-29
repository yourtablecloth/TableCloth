namespace TableCloth.ManagedAi;

// Results never trigger browser activity. Only a selection from the latest completed run can open.
public sealed class ManagedAiOrchestrator(IManagedAiProvider provider, ITableClothBrowser browser)
{
    private AiSearchResponse? _last;

    public async Task<AiSearchResponse> SearchAsync(AiSearchRequest request, IProgress<AiProgress>? progress,
        CancellationToken cancellationToken)
    {
        _last = null;
        _last = await provider.SearchAsync(request, progress, cancellationToken);
        return _last;
    }

    public Task OpenSelectionAsync(AiSearchCandidate candidate, CancellationToken cancellationToken)
    {
        if (_last is null || !_last.Results.Contains(candidate))
            throw new ManagedAiException(AiFailureCode.UnsafeUrl);
        return browser.OpenAsync(PublicHttpsUrl.Validate(candidate.TargetUrl.AbsoluteUri), cancellationToken);
    }
}

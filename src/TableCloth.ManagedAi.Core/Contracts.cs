using System.Diagnostics;

namespace TableCloth.ManagedAi;

public sealed record ManagedAiOptions
{
    public TimeSpan SoftTimeout { get; init; } = TimeSpan.FromSeconds(90);
    public TimeSpan HardTimeout { get; init; } = TimeSpan.FromSeconds(180);
    public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public long MaxDownloadBytes { get; init; } = 512L * 1024 * 1024;
    public long MaxExtractedBytes { get; init; } = 1024L * 1024 * 1024;
    public int MaxArchiveEntries { get; init; } = 2000;
    public int MaxStdoutBytes { get; init; } = 20 * 1024 * 1024;
    public int MaxLineBytes { get; init; } = 1024 * 1024;
    public int MaxStderrBytes { get; init; } = 1024 * 1024;
}

public enum AiFailureCode
{
    UnsupportedArchitecture, InvalidPath, RuntimeNotInstalled, RuntimeBusy,
    OfficialMetadataUnavailable, ReleaseFormatUnknown, IntegrityMismatch, ArchiveRejected,
    RuntimeVersionMismatch, BlockedByPolicy, AuthenticationRequired, ProviderTimeout,
    ProviderFailed, InvalidStructuredOutput, OutputLimitExceeded, UnsafeUrl,
    InvalidQuery, LiveSearchNotObserved, BrowserOpenFailed, NoPreviousVersion,
    SubscriptionUnavailable, ProviderNetworkUnavailable, ProviderConfigurationInvalid,
    ProviderRequestRejected, LoginFlowUnavailable, CatalogUnavailable, ModelListUnavailable, InvalidModel
}

// Only fixed codes cross the diagnostics/UI boundary. Never attach provider output or inner exceptions.
public sealed class ManagedAiException(AiFailureCode code) : Exception(code.ToString())
{
    public AiFailureCode Code { get; } = code;
}

public sealed record RuntimeCoordinate(string Version, string Target, string PackageSha256);
public sealed record ManagedRuntime(RuntimeCoordinate Coordinate, string RootDirectory,
    string EntryPoint, string ProfileDirectory, DateTimeOffset ActivatedAtUtc);
public sealed record AiSearchRequest(string Query, int MaxResults = 5);
public sealed record AiSearchResponse(string Answer, IReadOnlyList<AiSearchCandidate> Results,
    IReadOnlyList<string> Warnings, DateTimeOffset RetrievedAtUtc);
public sealed record AiSearchCandidate(string Title, Uri TargetUrl, string Description,
    string SourceName, Uri SourceUrl, string Reason);
public sealed record AiProgress(string Stage, int SearchCalls = 0);
public enum AiChatRole { User, Assistant }
public sealed record AiChatMessage(AiChatRole Role, string Text);
public sealed record AiChatRequest(string Message, IReadOnlyList<AiChatMessage> History, string? Model = null);
public sealed record AiChatResponse(string Text, DateTimeOffset RetrievedAtUtc, int SearchCalls, string? Model = null);
public sealed record AiModel(string Id, string DisplayName, bool IsDefault = false)
{
    public AiModelTokenCost? TokenCost { get; init; }
    public override string ToString() => DisplayName;
    public static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 128 || !char.IsAsciiLetterOrDigit(id[0]) ||
            id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ManagedAiException(AiFailureCode.InvalidModel);
    }
}
public interface IManagedAiModelCatalog
{
    Task<IReadOnlyList<AiModel>> ListAsync(CancellationToken cancellationToken);
}
public enum AiLoginMethod { DeviceCode, Browser }
public enum AiLoginStage { Starting, AwaitingUser, Completed }
public sealed record AiLoginUpdate(AiLoginStage Stage, Uri? VerificationUri = null, string? UserCode = null);
public sealed record ProcessRunSpec(ProcessStartInfo StartInfo, string? Input, TimeSpan Timeout,
    int MaxStdoutBytes, int MaxStderrBytes, int MaxLineBytes)
{
    // A bounded stdio exchange can keep stdin open until its final response arrives.
    public Func<string, ProcessInputReply?>? Respond { get; init; }
}
public sealed record ProcessInputReply(string? Text = null, bool Close = false);
public sealed record ProcessOutcome(int ExitCode, long StdoutBytes, long StderrBytes);

public interface IManagedAiProvider
{
    Task<AiSearchResponse> SearchAsync(AiSearchRequest request, IProgress<AiProgress>? progress,
        CancellationToken cancellationToken);
}
public interface IProviderAuthentication
{
    Task<bool> IsLoggedInAsync(CancellationToken cancellationToken);
    Task LoginAsync(CancellationToken cancellationToken);
    Task LoginAsync(AiLoginMethod method, IProgress<AiLoginUpdate>? progress, CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
}
public interface IManagedAiChatProvider
{
    Task<AiChatResponse> ChatAsync(AiChatRequest request, IProgress<AiProgress>? progress,
        CancellationToken cancellationToken);
}
public interface IManagedRuntimeManager
{
    Task<ManagedRuntime?> GetActiveAsync(CancellationToken cancellationToken);
    Task<ManagedRuntime> InstallAsync(string? version, IProgress<AiProgress>? progress, CancellationToken cancellationToken);
    Task<ManagedRuntime> RollbackAsync(CancellationToken cancellationToken);
}
public interface IJsonlProcessRunner
{
    Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout,
        Action<string>? stderr, CancellationToken cancellationToken);
}
public interface ITableClothBrowser
{
    Task OpenAsync(Uri target, CancellationToken cancellationToken);
}
public interface IManagedAiProfile
{
    void Prepare();
}

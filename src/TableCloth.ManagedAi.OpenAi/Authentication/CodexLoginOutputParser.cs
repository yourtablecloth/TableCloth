using System.Text.RegularExpressions;

namespace TableCloth.ManagedAi.OpenAi;

public sealed partial class CodexLoginOutputParser(AiLoginMethod method, IProgress<AiLoginUpdate>? progress)
{
    private readonly object _sync = new();
    private Uri? _verificationUri;
    private string? _userCode;
    public AiFailureCode Failure { get; private set; } = AiFailureCode.AuthenticationRequired;

    public void Accept(string line)
    {
        lock (_sync)
        {
            var text = AnsiEscape().Replace(line, string.Empty).Trim();
            var failure = CodexFailureClassifier.Classify(text);
            if (failure != AiFailureCode.ProviderFailed) Failure = failure;
            var previousUri = _verificationUri;
            var previousCode = _userCode;
            foreach (Match match in UrlPattern().Matches(text))
            {
                if (Uri.TryCreate(match.Value.TrimEnd('.', ',', ')'), UriKind.Absolute, out var uri) && IsAllowedLoginUri(uri))
                    _verificationUri = uri;
            }
            if (method == AiLoginMethod.DeviceCode && _verificationUri is not null)
            {
                var code = DeviceCode().Match(text);
                if (code.Success) _userCode = code.Value;
            }
            if (_verificationUri is not null && (previousUri != _verificationUri || previousCode != _userCode))
                progress?.Report(new(AiLoginStage.AwaitingUser, _verificationUri, _userCode));
        }
    }

    public static bool IsAllowedLoginUri(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "https" &&
        uri.IsDefaultPort && uri.IdnHost == "auth.openai.com" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 &&
        uri.AbsolutePath is "/codex/device" or "/device" or "/oauth/authorize" or "/authorize";

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.None, 100)]
    private static partial Regex AnsiEscape();
    [GeneratedRegex("https://[^\\s<>\"']+", RegexOptions.None, 100)]
    private static partial Regex UrlPattern();
    [GeneratedRegex(@"(?<![A-Za-z0-9])(?:[A-Z0-9]{4,5}-[A-Z0-9]{4,5}|[0-9]{3}-[0-9]{3}-[0-9]{3}|[0-9]{9})(?![A-Za-z0-9])", RegexOptions.None, 100)]
    private static partial Regex DeviceCode();
}

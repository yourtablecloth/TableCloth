namespace TableCloth.ManagedAi.OpenAi;

public static class CodexFailureClassifier
{
    // Inspect transient text in memory; persist/return only these fixed codes.
    public static AiFailureCode Classify(string? message)
    {
        if (string.IsNullOrEmpty(message)) return AiFailureCode.ProviderFailed;
        var text = message.ToLowerInvariant();
        if (Has("usage_limit", "rate_limit", "insufficient_quota", "usage limit", "rate limit", "quota exceeded", "billing"))
            return AiFailureCode.SubscriptionUnavailable;
        if (Has("device code", "device authorization", "device auth", "device_auth"))
            return AiFailureCode.LoginFlowUnavailable;
        if (Has("unauthorized", "token_expired", "token has expired", "refresh token", "401", "not logged in", "authentication"))
            return AiFailureCode.AuthenticationRequired;
        if (Has("unknown field", "unrecognized", "config.toml", "invalid configuration", "unexpected argument"))
            return AiFailureCode.ProviderConfigurationInvalid;
        if (Has("invalid_request", "invalid_json_schema", "invalid request", "unsupported", "not supported", "invalid schema", "model_not_found", "does not exist"))
            return AiFailureCode.ProviderRequestRejected;
        if (Has("error sending request", "connection", "connect error", "dns", "tls", "certificate", "websocket", "network", "timed out"))
            return AiFailureCode.ProviderNetworkUnavailable;
        return AiFailureCode.ProviderFailed;
        bool Has(params string[] values) => values.Any(text.Contains);
    }
}

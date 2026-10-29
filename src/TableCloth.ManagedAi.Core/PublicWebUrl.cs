namespace TableCloth.ManagedAi;

// Chat links may use either public web scheme. Local addresses and non-web schemes remain blocked.
public static class PublicWebUrl
{
    public static bool TryParse(string? value, out Uri uri) => PublicHttpsUrl.TryParseCore(value, true, out uri);
    public static Uri Validate(string value) => TryParse(value, out var uri)
        ? uri : throw new ManagedAiException(AiFailureCode.UnsafeUrl);
}

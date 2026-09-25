using System.Net;
using System.Net.Sockets;

namespace TableCloth.ManagedAi;

public static class PublicHttpsUrl
{
    public static bool TryParse(string? value, out Uri uri)
        => TryParseCore(value, false, out uri);

    internal static bool TryParseCore(string? value, bool allowHttp, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 ||
            value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\') ||
            !Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != "https" && !(allowHttp && parsed.Scheme == "http")) ||
            parsed.UserInfo.Length != 0 || parsed.IsLoopback || parsed.Host.Length == 0)
            return false;
        var host = parsed.IdnHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
            return false;
        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip))
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            if (IPAddress.IsLoopback(ip)) return false;
            var b = ip.GetAddressBytes();
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                if (b[0] is 0 or 10 or 127 || b[0] >= 224 ||
                    (b[0] == 169 && b[1] == 254) || (b[0] == 172 && b[1] is >= 16 and <= 31) ||
                    (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] is >= 64 and <= 127))
                    return false;
            }
            else if (ip.AddressFamily != AddressFamily.InterNetworkV6 ||
                (b[0] & 0xe0) != 0x20 || ip.ScopeId != 0)
                return false; // IPv6 global unicast only, excluding local/multicast/unspecified.
        }
        else if (parsed.HostNameType != UriHostNameType.Dns || !host.Contains('.')) return false;
        uri = parsed;
        return true;
    }

    public static Uri Validate(string value) => TryParse(value, out var uri)
        ? uri : throw new ManagedAiException(AiFailureCode.UnsafeUrl);
}

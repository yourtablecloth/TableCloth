using System.Text.RegularExpressions;

namespace TableCloth.ManagedAi.OpenAi;

public static class CertificateInputGuard
{
    private static readonly Regex DistinguishedName = new(
        @"(?im)(?:^|[\s;,])(?:CN|OU|O|SERIALNUMBER)\s*=\s*\S",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    private static readonly Regex CertificateFields = new(
        @"(?im)\b(?:subject|issuer|serial\s*number|thumbprint|fingerprint|주체명|발급자|일련번호)\s*[:=]\s*\S",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    private static readonly Regex ImageLink = new(
        @"!\[[^\]]*\]\([^)]*\)|data:image/|(?:https?://|file://|[A-Za-z]:\\)\S+\.(?:png|jpe?g|gif|webp|bmp)(?:[?#]\S*)?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
    private static readonly Regex CertificatePath = new(
        @"(?:[A-Za-z]:\\|\\\\|/)\S*(?:NPKI|signCert\.der|signPri\.key)|NPKI[\\/]\S+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));

    public static bool ContainsProhibitedMaterial(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try { return Matches(text); }
        catch (RegexMatchTimeoutException) { return true; }
    }

    private static bool Matches(string text)
    {
        if (ImageLink.IsMatch(text)) return true;
        if ((text.Contains("스크린샷", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("스크린 샷", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("캡처", StringComparison.OrdinalIgnoreCase)) &&
            (text.Contains("http://", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("https://", StringComparison.OrdinalIgnoreCase) ||
             text.Contains("file://", StringComparison.OrdinalIgnoreCase))) return true;
        if (text.Contains("-----BEGIN CERTIFICATE-----", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-----BEGIN PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-----BEGIN ENCRYPTED PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("-----BEGIN RSA PRIVATE KEY-----", StringComparison.OrdinalIgnoreCase) ||
            CertificatePath.IsMatch(text) ||
            DistinguishedName.IsMatch(text)) return true;

        var aboutCertificate = text.Contains("인증서", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("NPKI", StringComparison.OrdinalIgnoreCase);
        if (!aboutCertificate) return false;
        if (CertificateFields.IsMatch(text)) return true;
        return false;
    }
}

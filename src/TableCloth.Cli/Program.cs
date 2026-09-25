using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using TableCloth;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["certificates", "expiring", ..]) return Certificates(args[2..]);
            if (args is ["catalog", "services", ..]) return Catalog(args[2..]);
            Console.Error.WriteLine("Commands: certificates expiring [--within-days 30] [--with-catalog-summary] [--catalog-file PATH] [--details] [--root PATH]; catalog services [--query TEXT] [--limit 20] [--file PATH]");
            return args.Length == 0 || args[0] is "help" or "--help" ? 0 : 2;
        }
        catch (ArgumentException) { Console.Error.WriteLine("InvalidArguments"); return 2; }
        catch (Exception) { Console.Error.WriteLine("ReadFailed"); return 1; }
    }

    private static int Certificates(string[] args)
    {
        var days = 30;
        var details = false;
        var withCatalog = false;
        string? root = null;
        string? catalogFile = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--within-days" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed):
                    days = parsed; i++; break;
                case "--details": details = true; break;
                case "--with-catalog-summary": withCatalog = true; break;
                case "--root" when i + 1 < args.Length: root = args[++i]; break;
                case "--catalog-file" when i + 1 < args.Length: catalogFile = args[++i]; break;
                default: throw new ArgumentException("Unsupported option.");
            }
        }
        if (catalogFile is not null && (!withCatalog || !Path.IsPathFullyQualified(catalogFile) ||
            catalogFile.StartsWith("\\\\", StringComparison.Ordinal))) throw new ArgumentException("Invalid catalog path.");
        root ??= Path.Combine(NativeMethods.GetKnownFolderPath(NativeMethods.LocalLowFolderGuid), "NPKI");
        if (!Path.IsPathFullyQualified(root) || root.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("Invalid certificate root.");
        var result = new CertificateExpiryScanner().Scan(root, DateTimeOffset.Now, days, details);
        if (withCatalog) result.Catalog = ReadCatalogSummary(catalogFile ?? DefaultCatalogCache());
        Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CertificateExpiryReport));
        return result.UnreadableCertificates > 0 || result.SkippedDirectories > 0 ? 3 : 0;
    }

    private static int Catalog(string[] args)
    {
        var query = string.Empty;
        var limit = 20;
        string? catalogFile = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--query" when i + 1 < args.Length: query = args[++i]; break;
                case "--limit" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsed):
                    limit = parsed; i++; break;
                case "--file" when i + 1 < args.Length: catalogFile = args[++i]; break;
                default: throw new ArgumentException("Unsupported option.");
            }
        }
        if (query.Length > 100 || limit < 1 || limit > 100) throw new ArgumentException("Invalid query.");
        var cache = catalogFile ?? DefaultCatalogCache();
        if (!Path.IsPathFullyQualified(cache) || cache.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("Invalid catalog path.");
        if (File.Exists(cache) && (File.GetAttributes(cache) & FileAttributes.ReparsePoint) != 0)
            throw new ArgumentException("Catalog path is a reparse point.");
        if (!File.Exists(cache)) { Console.Error.WriteLine("CatalogCacheUnavailable"); return 4; }
        if (new FileInfo(cache).Length > 10 * 1024 * 1024) { Console.Error.WriteLine("CatalogCacheTooLarge"); return 4; }
        var settings = new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit };
        using var reader = XmlReader.Create(cache, settings);
        var document = XmlCatalogParser.ParseCatalogDocument(reader);
        var matches = document.Services.Where(service => string.IsNullOrWhiteSpace(query) ||
            service.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            service.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            service.Url.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            service.GetSearchKeywords().Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(limit).Select(service => new CatalogServiceSummary(service.Id, service.DisplayName, service.Url,
                service.Category.ToString(), service.PackageCountForDisplay)).ToArray();
        var result = new CatalogServicesReport(File.GetLastWriteTimeUtc(cache), document.Services.Count, matches);
        Console.WriteLine(JsonSerializer.Serialize(result, CliJsonContext.Default.CatalogServicesReport));
        return 0;
    }

    private static string DefaultCatalogCache() => CatalogCacheLocation.ForCurrentUser();

    private static CertificateCatalogSummary ReadCatalogSummary(string cache)
    {
        var result = new CertificateCatalogSummary();
        try
        {
            if (!File.Exists(cache) || new FileInfo(cache).Length > 10 * 1024 * 1024 ||
                (File.GetAttributes(cache) & FileAttributes.ReparsePoint) != 0) return result;
            var settings = new XmlReaderSettings { XmlResolver = null, DtdProcessing = DtdProcessing.Prohibit };
            using var reader = XmlReader.Create(cache, settings);
            result.ServiceCount = XmlCatalogParser.ParseCatalogDocument(reader).Services.Count;
            result.SnapshotModifiedAt = File.GetLastWriteTimeUtc(cache);
            result.CacheFound = true;
        }
        catch (Exception) { /* The certificate report remains useful when the cache is unavailable. */ }
        return result;
    }
}

public sealed record CatalogServiceSummary(string Id, string Name, string Url, string Category, int InstallationItems);
public sealed record CatalogServicesReport(DateTime SnapshotModifiedAtUtc, int TotalServices, CatalogServiceSummary[] Services);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CertificateExpiryReport))]
[JsonSerializable(typeof(CatalogServicesReport))]
internal partial class CliJsonContext : JsonSerializerContext;

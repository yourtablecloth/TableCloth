using System.Xml;
using System.Xml.Linq;

namespace TableCloth.ManagedAi.OpenAi;

public sealed class SkillCatalogClient(IHttpClientFactory clients)
{
    public const string HttpClientName = "TableCloth.SkillMarketplace";
    public const string CatalogUrl = "https://catalog.yourtablecloth.app/SkillCatalog.xml";
    public const int MaxCatalogBytes = 512 * 1024;

    public async Task<IReadOnlyList<SkillCatalogEntry>> ListAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            return Parse(await SkillMarketplaceHttp.GetAsync(clients, new(CatalogUrl), MaxCatalogBytes, timeout.Token));
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
            ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        { throw new ManagedAiException(AiFailureCode.CatalogUnavailable); }
    }

    public static IReadOnlyList<SkillCatalogEntry> Parse(ReadOnlyMemory<byte> xml)
    {
        if (xml.Length > MaxCatalogBytes) throw Invalid();
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = MaxCatalogBytes, MaxCharactersFromEntities = 0
            };
            // Bound nesting before XDocument constructs the tree.
            using (var bounded = XmlReader.Create(new MemoryStream(xml.ToArray()), settings))
                while (bounded.Read()) if (bounded.Depth > 3) throw Invalid();
            using var reader = XmlReader.Create(new MemoryStream(xml.ToArray()), settings);
            var root = XDocument.Load(reader).Root ?? throw Invalid();
            Shape(root, "SkillCatalog", ["Version", "{http://www.w3.org/2000/xmlns/}xsi",
                "{http://www.w3.org/2001/XMLSchema-instance}noNamespaceSchemaLocation"], ["Skill"]);
            if (Attribute(root, "Version") != "1") throw Invalid();
            var schemaNamespace = root.Attribute(XNamespace.Xmlns + "xsi")?.Value;
            var schemaLocation = root.Attribute(XName.Get("noNamespaceSchemaLocation", "http://www.w3.org/2001/XMLSchema-instance"))?.Value;
            if (schemaNamespace is not null && schemaNamespace != "http://www.w3.org/2001/XMLSchema-instance" ||
                schemaLocation is not null && schemaLocation != "SkillCatalog.xsd") throw Invalid();
            var elements = root.Elements().ToArray();
            if (elements.Length > 256) throw Invalid();
            var result = new List<SkillCatalogEntry>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var skill in elements)
            {
                Shape(skill, "Skill", ["Id", "Category", "DisplayName", "Status"],
                    ["Description", "Source", "Author", "License", "Requirements", "Install", "Notes"]);
                var id = Attribute(skill, "Id", 64);
                SkillCatalogValidation.ValidateId(id);
                if (!ids.Add(id)) throw Invalid();
                var source = Child(skill, "Source");
                Shape(source, "Source", ["Repository", "Path", "Ref"], []);
                var repository = Attribute(source, "Repository");
                SkillCatalogValidation.Repository(repository);
                var path = Attribute(source, "Path", 160);
                SkillCatalogValidation.Path(path, allowRoot: true);
                var sourceRef = Attribute(source, "Ref", 40);
                if (!SkillCatalogValidation.IsSha(sourceRef)) throw Invalid();
                var author = Child(skill, "Author", optional: true);
                if (author is not null) Shape(author, "Author", ["Name", "Url"], []);
                var license = Child(skill, "License");
                Shape(license, "License", ["SPDX", "ReviewStatus", "Source"], []);
                var requirements = Child(skill, "Requirements");
                Shape(requirements, "Requirements", ["Runtime", "Network"], []);
                var install = Child(skill, "Install");
                Shape(install, "Install", ["Mode", "ReviewStatus"], []);
                var runtime = Attribute(requirements, "Runtime", 32);
                if (runtime != "Codex") throw Invalid();
                var authorUrl = author is null ? string.Empty : Attribute(author, "Url", optional: true);
                var licenseSource = Attribute(license, "Source");
                SkillCatalogValidation.OptionalHttpsUrl(authorUrl);
                SkillCatalogValidation.OptionalHttpsUrl(licenseSource);
                result.Add(new(id, Attribute(skill, "Category", 64), Attribute(skill, "DisplayName", 128),
                    Text(Child(skill, "Description"), 4096, required: true), repository, path, sourceRef,
                    author is null ? string.Empty : Attribute(author, "Name", 128), Attribute(license, "SPDX", 128),
                    Choice(install, "Mode", ["upstream", "adapter"]),
                    Choice(requirements, "Network", ["required", "optional", "none"]),
                    Text(Child(skill, "Notes", optional: true), 8192))
                { AuthorUrl = authorUrl, LicenseSource = licenseSource, Runtime = runtime });
            }
            return result.AsReadOnly();
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException or ArgumentException)
        { throw Invalid(); }
    }

    private static XElement Child(XElement parent, string name, bool optional = false)
    {
        var children = parent.Elements(name).ToArray();
        if (children.Length > 1 || children.Length == 0 && !optional) throw Invalid();
        return children.SingleOrDefault()!;
    }

    private static void Shape(XElement element, string name, string[] attributes, string[] children)
    {
        if (element.Name != name || element.Attributes().Any(x => !attributes.Contains(x.Name.ToString(), StringComparer.Ordinal)) ||
            element.Elements().Any(x => !children.Contains(x.Name.ToString(), StringComparer.Ordinal)) ||
            element.Nodes().OfType<XText>().Any(x => !string.IsNullOrWhiteSpace(x.Value))) throw Invalid();
    }

    private static string Text(XElement? element, int maximum, bool required = false)
    {
        if (element is null) return string.Empty;
        if (element.HasAttributes || element.HasElements) throw Invalid();
        var value = element.Value.Trim();
        if (required && value.Length == 0 || value.Length > maximum || value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t')) throw Invalid();
        return value;
    }

    private static string Attribute(XElement element, string name, int maximum = 2048, bool optional = false)
    {
        var value = element.Attribute(name)?.Value ?? string.Empty;
        if (!optional && string.IsNullOrWhiteSpace(value) || value.Length > maximum || value != value.Trim() || value.Any(char.IsControl)) throw Invalid();
        return value;
    }

    private static string Choice(XElement element, string name, string[] choices)
    {
        var value = Attribute(element, name, 32);
        return choices.Contains(value, StringComparer.Ordinal) ? value : throw Invalid();
    }

    private static ManagedAiException Invalid() => new(AiFailureCode.SkillInvalid);
}

internal static class SkillCatalogValidation
{
    public static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || !char.IsAsciiLetterOrDigit(id[0]) ||
            id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.')) throw Invalid();
        Path(id);
    }

    public static bool IsSha(string value) => value.Length == 40 && value.All(char.IsAsciiHexDigit);

    public static (string Owner, string Name) Repository(string repository)
    {
        if (!Uri.TryCreate(repository, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.IdnHost != "github.com" ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            repository.Contains('%') || repository.Contains('\\')) throw Invalid();
        var parts = uri.AbsolutePath.Split('/');
        if (parts.Length != 3 || parts.Skip(1).Any(x => x.Length == 0 || x.Length > 100 ||
            x.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.') || x is "." or "..") ||
            parts[2].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ||
            !repository.Equals($"https://github.com/{parts[1]}/{parts[2]}", StringComparison.OrdinalIgnoreCase)) throw Invalid();
        return (parts[1], parts[2]);
    }

    public static void Path(string path, bool allowRoot = false)
    {
        if (allowRoot && path == ".") return;
        if (string.IsNullOrEmpty(path) || path.Length > 160 || path.Contains('\\') || path.Contains('%') ||
            path.Split('/').Any(x => x.Length == 0 || x.Length > 100 || x is "." or ".." ||
                x.EndsWith('.') || x.EndsWith(' ') || x.Any(c => char.IsControl(c) || "<>:\"|?*".Contains(c)) ||
                IsDeviceName(x))) throw Invalid();
    }

    private static bool IsDeviceName(string segment)
    {
        var stem = segment.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            (stem[3] is >= '0' and <= '9' or '\u00b9' or '\u00b2' or '\u00b3');
    }

    public static void OptionalHttpsUrl(string value)
    {
        if (value.Length != 0 && (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0)) throw Invalid();
    }

    private static ManagedAiException Invalid() => new(AiFailureCode.SkillInvalid);
}

internal static class SkillMarketplaceHttp
{
    public static async Task<byte[]> GetAsync(IHttpClientFactory clients, Uri url, int maximum, CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient(SkillCatalogClient.HttpClientName);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        // The named client disables redirects and ambient credentials; never follow response URLs.
        if (response.StatusCode != System.Net.HttpStatusCode.OK) throw new HttpRequestException();
        if (response.Content.Headers.ContentLength > maximum) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + count > maximum) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}

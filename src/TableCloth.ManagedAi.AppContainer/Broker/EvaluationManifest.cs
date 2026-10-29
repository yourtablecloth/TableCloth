using System.Security.Cryptography;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

// This is a trusted, local evaluation registration file, never a model-supplied document.
public sealed record EvaluationManifest(string OwnedRoot, string PythonExecutable, string PythonSha256,
    string? NodeExecutable, string? NodeSha256, IReadOnlyList<SkillRegistration> Skills,
    IReadOnlyList<RuntimeFileRegistration>? RuntimeFiles = null, bool UseSystemProxy = false);

public sealed record RuntimeFileRegistration(string Path, string Sha256);

public sealed record SkillRegistration(string SkillId, string Kind, string ScriptPath, string Sha256,
    IReadOnlyList<string> AllowedOperations, IReadOnlyList<string> FixedArguments, bool RequiresNetwork,
    string? PackageRoot = null, IReadOnlyList<RuntimeFileRegistration>? PackageFiles = null,
    string? Description = null, IReadOnlyList<string>? NetworkHosts = null);

public sealed class BrokerValidationException(string message) : Exception(message);

internal static class BrokerFiles
{
    internal const int MaximumManifestBytes = 4194304;
    internal const int MaximumInputBytes = 65536;
    internal static readonly string[] CalculatorOperations =
    [
        "percent.part", "percent.share", "percent.change", "percent.adjust", "percent.points",
        "discount.single", "discount.sequential", "discount.reverse", "unit_price", "bill_split",
        "interest.simple", "interest.compound", "savings.recurring", "savings.target", "loan",
        "date.diff", "date.add", "date.age", "date.weekday", "time.convert", "unit.convert", "bmi"
    ];

    internal static async Task<EvaluationManifest> ReadManifestAsync(string path, CancellationToken token)
    {
        if (!Path.IsPathFullyQualified(path)) throw Invalid();
        path = Path.GetFullPath(path);
        RequireLocalPath(path);
        RejectReparsePoints(path);
        var bytes = await ReadBoundedAsync(path, MaximumManifestBytes, token);
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        ValidateJson(document.RootElement, maximumNodes: 32768);
        var root = document.RootElement;
        ObjectFields(root, ["ownedRoot", "pythonExecutable", "pythonSha256", "nodeExecutable", "nodeSha256", "skills", "runtimeFiles", "useSystemProxy"]);
        var useSystemProxy = false;
        if (root.TryGetProperty("useSystemProxy", out var proxyValue))
        {
            if (proxyValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid();
            useSystemProxy = proxyValue.GetBoolean();
        }
        var ownedRoot = Text(root, "ownedRoot", 240);
        if (!Path.IsPathFullyQualified(ownedRoot) || ownedRoot.StartsWith(@"\\", StringComparison.Ordinal)) throw Invalid();
        ownedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ownedRoot));
        RequireLocalPath(ownedRoot);
        if (!Directory.Exists(ownedRoot) || string.Equals(ownedRoot, Path.GetPathRoot(ownedRoot), StringComparison.OrdinalIgnoreCase)) throw Invalid();
        RejectReparsePoints(ownedRoot);
        RequireUnder(ownedRoot, path);
        var python = ResolveFile(ownedRoot, Text(root, "pythonExecutable", 240));
        var pythonHash = Hash(root, "pythonSha256");
        string? node = OptionalText(root, "nodeExecutable", 240);
        string? nodeHash = OptionalText(root, "nodeSha256", 64);
        if ((node is null) != (nodeHash is null)) throw Invalid();
        if (node is not null) { node = ResolveFile(ownedRoot, node); ValidateHash(nodeHash!); }
        if (!root.TryGetProperty("skills", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() is < 1 or > 128) throw Invalid();
        var skills = new List<SkillRegistration>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packageFileCount = 0;
        foreach (var item in list.EnumerateArray())
        {
            ObjectFields(item, ["skillId", "kind", "scriptPath", "sha256", "allowedOperations", "fixedArguments", "requiresNetwork", "packageRoot", "packageFiles", "description", "networkHosts"]);
            var id = Text(item, "skillId", 64);
            if (!names.Add(id) || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')) || !char.IsAsciiLetterOrDigit(id[0])) throw Invalid();
            var kind = Text(item, "kind", 32);
            if (kind is not ("python-json-stdin" or "node-json-stdin" or "python-cli-fixed" or "python-script" or "node-script")) throw Invalid();
            var script = ResolveFile(ownedRoot, Text(item, "scriptPath", 240));
            var hash = Hash(item, "sha256");
            var operations = Strings(item, "allowedOperations", 32, 64);
            var arguments = Strings(item, "fixedArguments", 32, 256, unique: false);
            var packageRoot = OptionalText(item, "packageRoot", 240);
            var description = OptionalText(item, "description", 4096);
            var networkHosts = Strings(item, "networkHosts", 2, 128);
            if (networkHosts.Any(x => !SkillNetworkBridge.KnownHosts.Contains(x, StringComparer.Ordinal))) throw Invalid();
            IReadOnlyList<RuntimeFileRegistration>? packageFiles = null;
            var network = false;
            if (item.TryGetProperty("requiresNetwork", out var networkValue))
            {
                if (networkValue.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid();
                network = networkValue.GetBoolean();
            }
            if (kind is "python-script" or "node-script")
            {
                if (arguments.Count != 0 || operations.Count != 0 || packageRoot is null ||
                    kind == "node-script" && node is null) throw Invalid();
                packageRoot = Path.GetFullPath(packageRoot, ownedRoot);
                RequireUnder(ownedRoot, packageRoot);
                RejectReparsePoints(packageRoot);
                if (!Directory.Exists(packageRoot)) throw Invalid();
                RequireUnder(packageRoot, script);
                var extension = Path.GetExtension(script);
                if (kind == "python-script" ? !extension.Equals(".py", StringComparison.OrdinalIgnoreCase) :
                    !new[] { ".js", ".cjs", ".mjs" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) throw Invalid();
                if (!item.TryGetProperty("packageFiles", out var fileList) || fileList.ValueKind != JsonValueKind.Array ||
                    fileList.GetArrayLength() is < 1 or > 256 || (packageFileCount += fileList.GetArrayLength()) > 4096) throw Invalid();
                var files = new List<RuntimeFileRegistration>();
                var uniqueFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in fileList.EnumerateArray())
                {
                    ObjectFields(file, ["path", "sha256"]);
                    var filePath = ResolveFile(ownedRoot, Text(file, "path", 240));
                    RequireUnder(packageRoot, filePath);
                    if (!uniqueFiles.Add(filePath)) throw Invalid();
                    files.Add(new(filePath, Hash(file, "sha256")));
                }
                if (!files.Any(x => x.Path.Equals(script, StringComparison.OrdinalIgnoreCase) &&
                    x.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase))) throw Invalid();
                packageFiles = files.AsReadOnly();
                ValidatePackageContents(packageRoot, packageFiles);
            }
            else if (packageRoot is not null || item.TryGetProperty("packageFiles", out _) || description is not null) throw Invalid();
            else if (kind == "python-json-stdin")
            {
                if (arguments.Count != 0 || operations.Any(x => !CalculatorOperations.Contains(x, StringComparer.Ordinal))) throw Invalid();
                if (operations.Count == 0) operations = Array.AsReadOnly(CalculatorOperations);
            }
            else if (kind == "node-json-stdin")
            {
                if (node is null || arguments.Count != 0 || operations.Count != 0) throw Invalid();
            }
            else if (operations.Count != 0 || arguments.Count == 0) throw Invalid();
            if (networkHosts.Count != 0 && (!network || kind is not ("python-script" or "node-script"))) throw Invalid();
            skills.Add(new(id, kind, script, hash, operations, arguments, network, packageRoot, packageFiles, description, networkHosts));
        }
        var runtimeFiles = new List<RuntimeFileRegistration>();
        if (root.TryGetProperty("runtimeFiles", out var dependencies))
        {
            if (dependencies.ValueKind != JsonValueKind.Array || dependencies.GetArrayLength() > 1024) throw Invalid();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dependency in dependencies.EnumerateArray())
            {
                ObjectFields(dependency, ["path", "sha256"]);
                var dependencyPath = ResolveFile(ownedRoot, Text(dependency, "path", 240));
                if (!unique.Add(dependencyPath)) throw Invalid();
                runtimeFiles.Add(new(dependencyPath, Hash(dependency, "sha256")));
            }
        }
        // Validate executable, dependencies and script content before advertising any registration.
        await using (await OpenVerifiedAsync(ownedRoot, python, pythonHash, token)) { }
        if (node is not null) { await using var ignored = await OpenVerifiedAsync(ownedRoot, node, nodeHash!, token); }
        foreach (var skill in skills)
        {
            await using var ignored = await OpenVerifiedAsync(ownedRoot, skill.ScriptPath, skill.Sha256, token);
            await using var package = await OpenPackageFilesAsync(ownedRoot, skill, token);
        }
        await using (await OpenRuntimeFilesAsync(ownedRoot, runtimeFiles, token)) { }
        return new(ownedRoot, python, pythonHash, node, nodeHash, skills.AsReadOnly(), runtimeFiles.AsReadOnly(), useSystemProxy);
    }

    internal static async Task<IAsyncDisposable> OpenPackageFilesAsync(string root, SkillRegistration skill, CancellationToken token)
    {
        if (skill.PackageRoot is not null) ValidatePackageContents(skill.PackageRoot, skill.PackageFiles!);
        var handles = new RuntimeFileLocks();
        try
        {
            if (skill.PackageFiles is not null)
                foreach (var file in skill.PackageFiles)
                    handles.Add(await OpenVerifiedAsync(root, file.Path, file.Sha256, token, allowEmpty: true));
            return handles;
        }
        catch { await handles.DisposeAsync(); throw; }
    }

    private static void ValidatePackageContents(string root, IReadOnlyList<RuntimeFileRegistration> files)
    {
        var expected = new HashSet<string>(files.Select(x => x.Path), StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        var entries = 0;
        while (pending.TryPop(out var current))
        {
            RejectReparsePoints(current);
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                if (++entries > 1024) throw Invalid();
                RequireUnder(root, entry);
                RejectReparsePoints(entry);
                if (Directory.Exists(entry)) pending.Push(entry);
                else if (!expected.Remove(entry)) throw Invalid();
            }
        }
        if (expected.Count != 0) throw Invalid();
    }

    internal static async Task<IAsyncDisposable> OpenRuntimeFilesAsync(string root,
        IReadOnlyList<RuntimeFileRegistration>? files, CancellationToken token)
    {
        var handles = new RuntimeFileLocks();
        try
        {
            if (files is not null)
                foreach (var file in files) handles.Add(await OpenVerifiedAsync(root, file.Path, file.Sha256, token));
            return handles;
        }
        catch { await handles.DisposeAsync(); throw; }
    }

    private sealed class RuntimeFileLocks : IAsyncDisposable
    {
        private readonly List<FileStream> _files = [];
        internal void Add(FileStream file) => _files.Add(file);
        public async ValueTask DisposeAsync()
        {
            foreach (var file in _files) await file.DisposeAsync();
            _files.Clear();
        }
    }

    internal static async Task<FileStream> OpenVerifiedAsync(string root, string path, string expectedHash, CancellationToken token,
        bool allowEmpty = false)
    {
        RequireUnder(root, path);
        RejectReparsePoints(path);
        // Keep the handle open through worker completion. On Windows this denies modification/deletion.
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        try
        {
            if (file.Length < (allowEmpty ? 0 : 1) || file.Length > 268435456) throw Invalid();
            var hash = await SHA256.HashDataAsync(file, token);
            if (!Convert.ToHexString(hash).Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw Invalid();
            file.Position = 0;
            return file;
        }
        catch { await file.DisposeAsync(); throw; }
    }

    internal static string ResolveFile(string root, string supplied)
    {
        var path = Path.GetFullPath(supplied, root);
        RequireUnder(root, path);
        RejectReparsePoints(path);
        if (!File.Exists(path)) throw Invalid();
        return path;
    }

    internal static void RequireUnder(string root, string path)
    {
        var normalized = Path.GetFullPath(path);
        RequireLocalPath(normalized);
        if (!normalized.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase) || normalized.Length > 240) throw Invalid();
    }

    private static void RequireLocalPath(string normalized)
    {
        // A drive separator is the only permitted colon. Reject ADS before any file is opened.
        if (normalized.Length < 3 || normalized[1] != ':' || !char.IsAsciiLetter(normalized[0]) ||
            normalized.StartsWith(@"\\", StringComparison.Ordinal) || normalized.IndexOf(':', 2) >= 0) throw Invalid();
    }

    internal static void RejectReparsePoints(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Invalid();
    }

    internal static void ValidateJson(JsonElement value, int maximumNodes = 8192)
    {
        var budget = maximumNodes;
        Visit(value, ref budget);
    }

    internal static void ValidateCalculatorInput(JsonElement input, string operation)
    {
        var (required, optional) = operation switch
        {
            "percent.part" => (new[] { "total", "rate_percent" }, Array.Empty<string>()),
            "percent.share" => (new[] { "part", "total" }, Array.Empty<string>()),
            "percent.change" => (new[] { "before", "after" }, Array.Empty<string>()),
            "percent.adjust" => (new[] { "value", "rate_percent" }, Array.Empty<string>()),
            "percent.points" => (new[] { "before_percent", "after_percent" }, Array.Empty<string>()),
            "discount.single" or "discount.reverse" => (new[] { "price", "rate_percent" }, new[] { "places" }),
            "discount.sequential" => (new[] { "price", "rates_percent" }, new[] { "places" }),
            "unit_price" => (new[] { "total", "quantity" }, new[] { "per_quantity", "places" }),
            "bill_split" => (new[] { "total", "weights" }, new[] { "places" }),
            "interest.simple" => (new[] { "principal", "annual_rate_percent", "years" }, new[] { "places" }),
            "interest.compound" => (new[] { "principal", "annual_rate_percent", "years" }, new[] { "places", "compounds_per_year" }),
            "savings.recurring" => (new[] { "initial", "contribution", "annual_rate_percent", "periods", "periods_per_year" }, new[] { "timing", "places" }),
            "savings.target" => (new[] { "initial", "target", "annual_rate_percent", "periods", "periods_per_year" }, new[] { "timing", "places" }),
            "loan" => (new[] { "principal", "annual_rate_percent", "periods", "periods_per_year", "method" }, new[] { "schedule", "places" }),
            "date.diff" => (new[] { "start", "end" }, new[] { "inclusion" }),
            "date.add" => (new[] { "date" }, new[] { "years", "months", "days" }),
            "date.age" => (new[] { "birth_date", "as_of" }, new[] { "leap_day" }),
            "date.weekday" => (new[] { "date" }, Array.Empty<string>()),
            "time.convert" => (new[] { "datetime", "from_offset", "to_offset" }, Array.Empty<string>()),
            "unit.convert" => (new[] { "value", "from_unit", "to_unit" }, Array.Empty<string>()),
            "bmi" => (new[] { "weight_kg", "height_cm" }, Array.Empty<string>()),
            _ => throw Invalid()
        };
        ObjectFields(input, ["operation", .. required, .. optional]);
        foreach (var field in required) if (!input.TryGetProperty(field, out _)) throw Invalid();
    }

    private static void Visit(JsonElement value, ref int budget)
    {
        if (--budget < 0) throw Invalid();
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw Invalid();
                Visit(property.Value, ref budget);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) Visit(item, ref budget);
    }

    internal static void ObjectFields(JsonElement value, string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Any(x => !names.Contains(x.Name, StringComparer.Ordinal))) throw Invalid();
    }

    internal static string Text(JsonElement value, string name, int maximum)
        => OptionalText(value, name, maximum) ?? throw Invalid();

    private static string? OptionalText(JsonElement value, string name, int maximum)
    {
        if (!value.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null) return null;
        if (property.ValueKind != JsonValueKind.String) throw Invalid();
        var text = property.GetString()!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text.Any(char.IsControl)) throw Invalid();
        return text;
    }

    private static string Hash(JsonElement root, string name)
    {
        var value = Text(root, name, 64);
        ValidateHash(value);
        return value;
    }

    private static void ValidateHash(string value)
    {
        if (value.Length != 64 || !value.All(char.IsAsciiHexDigit)) throw Invalid();
    }

    private static IReadOnlyList<string> Strings(JsonElement value, string name, int maximumCount, int maximumLength, bool unique = true)
    {
        if (!value.TryGetProperty(name, out var list)) return Array.Empty<string>();
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > maximumCount) throw Invalid();
        var result = new List<string>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw Invalid();
            var text = item.GetString()!;
            if (string.IsNullOrEmpty(text) || text.Length > maximumLength || text.Any(char.IsControl)) throw Invalid();
            result.Add(text);
        }
        if (unique && result.Distinct(StringComparer.Ordinal).Count() != result.Count) throw Invalid();
        return result.AsReadOnly();
    }

    private static async Task<byte[]> ReadBoundedAsync(string path, int maximum, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await file.ReadAsync(chunk, token)) != 0)
        {
            if (buffer.Length + count > maximum) throw Invalid();
            buffer.Write(chunk, 0, count);
        }
        return buffer.ToArray();
    }

    internal static BrokerValidationException Invalid() => new("Invalid evaluation registration or input.");
}

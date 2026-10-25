using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

internal static class Preparation
{
    internal static async Task<int> RunAsync(string workspace, CancellationToken token)
    {
        workspace = Path.GetFullPath(workspace);
        var destination = Path.Combine(workspace, "Artifacts", "appcontainer-skill-poc");
        var sources = Path.Combine(workspace, "Artifacts", "skill-runtime-research-20261025");
        var pythonZip = Path.Combine(sources, "python-3.14.9-embed-amd64.zip");
        var nodeZip = Path.Combine(sources, "node-v24.22.1-win-x64.zip");
        await EnsureArchiveAsync(pythonZip, "https://www.python.org/ftp/python/3.14.9/python-3.14.9-embed-amd64.zip", "3c01d7cfc666f746a6f064f60fb2156ab0ec464d3d738ab796371a46a9bb24a2", token);
        await EnsureArchiveAsync(nodeZip, "https://nodejs.org/dist/v24.22.1/node-v24.22.1-win-x64.zip", "d2046d28c2c3dcd211ac5145c0bf30e2047ef9dfb306693c8c6e2eccd8b95fa5", token);
        Directory.CreateDirectory(destination);
        var owned = new OwnedPaths(destination);
        using var lease = owned.AcquireAclLease();
        if (Directory.EnumerateFiles(destination, ".acl-journal-*.json").Any()) throw new InvalidOperationException("Recover pending ACL journals before preparation.");
        foreach (var temporaryDirectory in new[] { "probe-work", "public-api-work" })
            owned.DeleteRunDirectory(Under(destination, temporaryDirectory));
        await ExtractAsync(pythonZip, Path.Combine(destination, "runtime", "python"), "", token);
        await ExtractAsync(nodeZip, Path.Combine(destination, "runtime", "node"), "node-v24.22.1-win-x64/", token);
        CopyTree(Path.Combine(workspace, "tools", "appcontainer-skill-poc", "skills"), Path.Combine(destination, "skills"));
        CopyTree(Path.Combine(workspace, "tools", "appcontainer-skill-poc", "Probes"), Path.Combine(destination, "probes"));
        var python = Path.Combine(destination, "runtime", "python", "python.exe");
        var node = Path.Combine(destination, "runtime", "node", "node.exe");
        var calculator = Path.Combine(destination, "skills", "tablecloth-calculators", "scripts", "calculate.py");
        var kosis = Path.Combine(destination, "skills", "kosis-stats", "scripts", "run_kosis_stats.py");
        var bok = Path.Combine(destination, "skills", "bok-ecos-stats", "scripts", "bok_ecos.py");
        var manifest = new
        {
            ownedRoot = destination, pythonExecutable = "runtime/python/python.exe", pythonSha256 = Hash(python),
            nodeExecutable = "runtime/node/node.exe", nodeSha256 = Hash(node),
            skills = new object[]
            {
                new { skillId = "tablecloth-calculators", kind = "python-json-stdin", scriptPath = "skills/tablecloth-calculators/scripts/calculate.py", sha256 = Hash(calculator),
                    allowedOperations = new[] { "discount.single", "percent.part", "date.add", "time.convert", "bmi", "loan" }, requiresNetwork = false },
                new { skillId = "kosis-stats", kind = "python-cli-fixed", scriptPath = "skills/kosis-stats/scripts/run_kosis_stats.py", sha256 = Hash(kosis),
                    fixedArguments = new[] { "search", "--query", "인구", "--result-count", "1", "--json", "--dry-run" }, requiresNetwork = false },
                new { skillId = "bok-ecos-stats", kind = "python-cli-fixed", scriptPath = "skills/bok-ecos-stats/scripts/bok_ecos.py", sha256 = Hash(bok),
                    fixedArguments = new[] { "key", "--limit", "1" }, requiresNetwork = true },
                new { skillId = "node-runtime-fixture", kind = "node-json-stdin", scriptPath = "probes/node-json.cjs", sha256 = Hash(Path.Combine(destination, "probes", "node-json.cjs")), requiresNetwork = false }
            }
        };
        await File.WriteAllTextAsync(Path.Combine(destination, "evaluation-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), token);
        Console.WriteLine("Prepared isolated PoC runtimes and pinned skills: " + destination);
        return 0;
    }
    internal static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static async Task EnsureArchiveAsync(string path, string url, string expected, CancellationToken token)
    {
        RejectReparse(path);
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var partial = path + ".download-" + Guid.NewGuid().ToString("N");
            try
            {
                using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(3) };
                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > 100_000_000) throw new InvalidDataException("Runtime archive too large");
                await using (var input = await response.Content.ReadAsStreamAsync(token))
                await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[65536]; long size = 0; int count;
                    while ((count = await input.ReadAsync(buffer, token)) != 0)
                    {
                        if ((size += count) > 100_000_000) throw new InvalidDataException("Runtime archive too large");
                        await output.WriteAsync(buffer.AsMemory(0, count), token);
                    }
                }
                Verify(partial, expected);
                File.Move(partial, path);
            }
            finally { if (File.Exists(partial)) File.Delete(partial); }
        }
        Verify(path, expected);
    }
    private static void Verify(string path, string expected)
    {
        RejectReparse(path);
        if (Hash(path) != expected) throw new InvalidDataException("Runtime archive hash mismatch: " + Path.GetFileName(path));
    }
    private static async Task ExtractAsync(string archive, string directory, string prefix, CancellationToken token)
    {
        Directory.CreateDirectory(directory); RejectReparse(directory);
        using var zip = ZipFile.OpenRead(archive);
        long expanded = 0;
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName.Replace('\\', '/');
            if (prefix.Length != 0 && !name.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Archive prefix mismatch");
            name = name[prefix.Length..];
            if (name.Length == 0) continue;
            if (name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(x => x == ".." || x == ".")) throw new InvalidDataException("Unsafe archive path");
            if ((expanded += entry.Length) > 600L * 1024 * 1024) throw new InvalidDataException("Archive too large");
            var target = Under(directory, name.Replace('/', Path.DirectorySeparatorChar));
            RejectReparse(target);
            if (name.EndsWith('/')) Directory.CreateDirectory(target);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = entry.Open(); await using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output, token);
            }
        }
    }
    private static void CopyTree(string source, string destination)
    {
        RejectReparse(source); Directory.CreateDirectory(destination); RejectReparse(destination);
        foreach (var child in Directory.EnumerateFileSystemEntries(source))
        {
            RejectReparse(child);
            var target = Under(destination, Path.GetFileName(child));
            if (Directory.Exists(child)) CopyTree(child, target);
            else { RejectReparse(target); File.Copy(child, target, true); }
        }
    }
    internal static string Under(string root, string path)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var target = Path.GetFullPath(Path.Combine(root, path));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Path escaped owned directory");
        RejectReparse(target); return target;
    }
    internal static void RejectReparse(string path)
    {
        for (var node = new DirectoryInfo(Path.GetFullPath(path)); node is not null; node = node.Parent)
            if ((File.Exists(node.FullName) || Directory.Exists(node.FullName)) && (File.GetAttributes(node.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Reparse point is not allowed");
    }
}

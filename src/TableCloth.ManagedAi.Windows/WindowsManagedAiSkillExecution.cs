using System.Security.Cryptography;
using System.Text.Json;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Windows;

// Only app-reviewed, hash-pinned registrations can expose scripts. A catalog entry cannot register an executable.
public sealed class WindowsManagedAiSkillExecution(ManagedAiPaths paths,
    WindowsManagedAiScriptRuntimeManager? runtimeManager = null, string? hostExecutable = null) : IManagedAiSkillExecution
{
    public const string CalculatorSha256 = "279b627d4350b8895717a6ac3597a6e87cab7a9882004d639251ebbee3e7c947";
    public const string CalculatorId = "tablecloth-calculators";

    public async Task<AiSkillExecutionBridge?> PrepareAsync(IReadOnlyList<AiSkill> skills,
        string runDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var installed = skills.SingleOrDefault(x => x.Id == CalculatorId && x.Enabled && x.IsValid);
        if (installed is null) return null;
        string? pendingManifest = null;
        try
        {
            var expectedSkill = paths.Under("profiles", "openai-codex", "codex-home", "skills", CalculatorId);
            if (!Path.GetFullPath(installed.Directory).Equals(expectedSkill, StringComparison.OrdinalIgnoreCase)) throw Blocked();
            ManagedAiPaths.RejectReparsePoints(runDirectory);
            var root = paths.Under("skill-execution");
            var host = hostExecutable ?? ResolveHostExecutable(Environment.ProcessPath, AppContext.BaseDirectory);
            if (host is null || !Path.IsPathFullyQualified(host) ||
                !Path.GetFileName(host).Equals("TableCloth.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(host))
                throw new ManagedAiException(AiFailureCode.SkillExecutionHostUnavailable);
            ManagedAiPaths.RejectReparsePoints(host);
            var source = Path.Combine(expectedSkill, "scripts", "calculate.py");
            ManagedAiPaths.RejectReparsePoints(source);
            // Keep the source locked while copying. A changed skill version needs explicit adapter review.
            await using var locked = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            var scriptHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(locked, cancellationToken));
            if (scriptHash != CalculatorSha256) throw Blocked();
            var manager = runtimeManager ?? new WindowsManagedAiScriptRuntimeManager(paths);
            var status = await manager.EnsureInstalledAsync(cancellationToken);
            if (!status.Python.Installed || !status.Node.Installed) throw new ManagedAiException(AiFailureCode.SkillIsolationFailed);
            var runtimeFiles = await manager.GetVerifiedFilesAsync(cancellationToken);
            var python = manager.PythonExecutable;
            var node = manager.NodeExecutable;
            var pythonHash = runtimeFiles.Single(x => x.Path.Equals(python, StringComparison.OrdinalIgnoreCase)).Sha256;
            var nodeHash = runtimeFiles.Single(x => x.Path.Equals(node, StringComparison.OrdinalIgnoreCase)).Sha256;
            locked.Position = 0;
            var script = paths.Under("skill-execution", "skills", CalculatorId, "scripts", "calculate.py");
            Directory.CreateDirectory(Path.GetDirectoryName(script)!);
            await using (var destination = new FileStream(script, FileMode.Create, FileAccess.Write, FileShare.None))
                await locked.CopyToAsync(destination, cancellationToken);
            // A unique manifest prevents concurrent chats from replacing another broker's registration.
            var manifest = paths.Under("skill-execution", "manifest-" + Guid.NewGuid().ToString("N") + ".json");
            pendingManifest = manifest;
            using (var file = new FileStream(manifest, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(file))
            {
                writer.WriteStartObject();
                writer.WriteString("ownedRoot", root);
                writer.WriteString("pythonExecutable", python);
                writer.WriteString("pythonSha256", pythonHash);
                writer.WriteString("nodeExecutable", node);
                writer.WriteString("nodeSha256", nodeHash);
                writer.WriteStartArray("runtimeFiles");
                foreach (var fileEntry in runtimeFiles)
                {
                    writer.WriteStartObject(); writer.WriteString("path", fileEntry.Path);
                    writer.WriteString("sha256", fileEntry.Sha256); writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WritePropertyName("skills"); writer.WriteStartArray(); writer.WriteStartObject();
                writer.WriteString("skillId", CalculatorId);
                writer.WriteString("kind", "python-json-stdin");
                writer.WriteString("scriptPath", script);
                writer.WriteString("sha256", CalculatorSha256);
                writer.WritePropertyName("allowedOperations"); writer.WriteStartArray();
                foreach (var operation in new[] { "discount.single", "percent.part", "date.add", "time.convert", "bmi", "loan" }) writer.WriteStringValue(operation);
                writer.WriteEndArray(); writer.WriteBoolean("requiresNetwork", false);
                writer.WriteEndObject(); writer.WriteEndArray(); writer.WriteEndObject();
            }
            // Use the same manifest validation that the actual subprocess repeats before advertising tools.
            await SkillExecutionRegistry.LoadAsync(manifest, cancellationToken);
            var bridge = new AiSkillExecutionBridge(host, ["--skill-mcp", manifest], root, [CalculatorId]);
            pendingManifest = null;
            return bridge;
        }
        catch (OperationCanceledException) { throw; }
        catch (ManagedAiException) { throw; }
        catch { throw Blocked(); }
        finally
        {
            if (pendingManifest is not null)
                try { ManagedAiPaths.RejectReparsePoints(pendingManifest); File.Delete(pendingManifest); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ManagedAiException) { }
        }
    }

    internal static string? ResolveHostExecutable(string? processPath, string applicationDirectory)
    {
        // Framework-dependent launches run as dotnet.exe. Only the fixed apphost beside
        // the product payload can serve MCP; never search PATH or launch dotnet/shell tools.
        if (processPath is not null && Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return Path.IsPathFullyQualified(applicationDirectory) ? Path.Combine(applicationDirectory, "TableCloth.exe") : null;
        return processPath;
    }

    private static ManagedAiException Blocked() => new(AiFailureCode.BlockedByPolicy);
}

using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace TableCloth.ManagedAi.OpenAi;

public sealed class SkillRuntimeDiagnostics : IManagedAiSkillRuntimeDiagnostics
{
    private const string SupportedToken = "TABLECLOTH_LONGPATH_SUPPORTED";
    private const string LimitedToken = "TABLECLOTH_LONGPATH_LIMITED";
    private readonly ManagedAiPaths _paths;
    private readonly IManagedAiSkillRuntimeManager _runtimes;
    private readonly IJsonlProcessRunner _runner;
    private readonly Func<WindowsLongPathPolicy> _policyReader;

    public SkillRuntimeDiagnostics(ManagedAiPaths paths, IManagedAiSkillRuntimeManager runtimes,
        IJsonlProcessRunner runner, Func<WindowsLongPathPolicy>? policyReader = null)
    {
        _paths = paths; _runtimes = runtimes; _runner = runner;
        _policyReader = policyReader ?? ReadWindowsPolicy;
    }

    public async Task<SkillRuntimeDiagnosticsResult> CheckAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WindowsLongPathPolicy policy;
        try { policy = _policyReader(); }
        catch (Exception error) when (IsDiagnosticFailure(error)) { policy = WindowsLongPathPolicy.Unknown; }

        SkillRuntimeEnvironment? active = null;
        var runtimeStatus = LongPathSupport.NotInstalled;
        try { active = await _runtimes.GetActiveAsync(cancellationToken); }
        catch (Exception error) when (IsDiagnosticFailure(error)) { runtimeStatus = LongPathSupport.Unknown; }
        cancellationToken.ThrowIfCancellationRequested();
        if (active is not null) runtimeStatus = LongPathSupport.Unknown;
        var managed = LongPathSupport.Unknown;
        var node = runtimeStatus;
        var python = runtimeStatus;
        string? probe = null;
        var created = false;
        try
        {
            probe = _paths.Under("profiles", "skill-tools", "diagnostics", Guid.NewGuid().ToString("N"));
            if (Directory.Exists(probe) || File.Exists(probe)) throw new ManagedAiException(AiFailureCode.InvalidPath);
            Directory.CreateDirectory(probe);
            created = true;
            foreach (var name in new[] { "home", "temp" }) Directory.CreateDirectory(ProbePath(probe, name));
            managed = CheckManagedIo(probe, cancellationToken);
            if (active is not null)
            {
                node = await CheckRuntimeAsync(active.NodeJs, "node", NodeScript, probe, cancellationToken);
                python = await CheckRuntimeAsync(active.Python, "python", PythonScript, probe, cancellationToken);
            }
        }
        catch (Exception error) when (IsDiagnosticFailure(error)) { }
        finally
        {
            // The runner awaits process termination before returning or propagating cancellation.
            // Never delete a stale/shared directory or follow a replaced probe root.
            if (created)
            {
                try { _paths.DeleteOwnedDirectory(probe!); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException ||
                    error is ManagedAiException { Code: AiFailureCode.InvalidPath }) { }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new(policy, managed, node, python);
    }

    private static WindowsLongPathPolicy ReadWindowsPolicy()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393)) return WindowsLongPathPolicy.Unsupported;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\FileSystem", writable: false);
            return key?.GetValue("LongPathsEnabled") switch
            {
                null => WindowsLongPathPolicy.Disabled,
                int value => value == 1 ? WindowsLongPathPolicy.Enabled : WindowsLongPathPolicy.Disabled,
                _ => WindowsLongPathPolicy.Unknown
            };
        }
        catch (Exception error) when (error is UnauthorizedAccessException or SecurityException or IOException)
        { return WindowsLongPathPolicy.Unknown; }
    }

    private static LongPathSupport CheckManagedIo(string probe, CancellationToken token)
    {
        try { ProbeManaged(ProbePath(probe, "managed-short"), token); }
        catch (Exception error) when (IsDiagnosticFailure(error)) { return LongPathSupport.Unknown; }
        try
        {
            ProbeManaged(LongProbePath(probe, "managed"), token);
            return LongPathSupport.Supported;
        }
        catch (PathTooLongException) { return LongPathSupport.Limited; }
        catch (IOException error) when ((error.HResult & 0xffff) == 206) { return LongPathSupport.Limited; }
        catch (Exception error) when (IsDiagnosticFailure(error)) { return LongPathSupport.Unknown; }
    }

    private static void ProbeManaged(string directory, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ManagedAiPaths.RejectReparsePoints(directory);
        Directory.CreateDirectory(directory);
        var original = Path.Combine(directory, "probe.txt");
        var renamed = Path.Combine(directory, "renamed.txt");
        using (var file = new FileStream(original, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            file.Write("tablecloth"u8);
        if (File.ReadAllText(original) != "tablecloth") throw new IOException();
        token.ThrowIfCancellationRequested();
        File.Move(original, renamed);
        if (File.ReadAllText(renamed) != "tablecloth") throw new IOException();
        File.Delete(renamed);
    }

    private async Task<LongPathSupport> CheckRuntimeAsync(SkillRuntimeInstallation installation, string runtime,
        string script, string probe, CancellationToken token)
    {
        try
        {
            if (!IsPrivateInstallation(installation, runtime)) return LongPathSupport.Unknown;
            var start = new ProcessStartInfo(installation.EntryPoint)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false, true),
                StandardErrorEncoding = new UTF8Encoding(false, true), WorkingDirectory = probe
            };
            start.Environment.Clear();
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            start.Environment["SystemRoot"] = windows;
            start.Environment["WINDIR"] = windows;
            start.Environment["TEMP"] = ProbePath(probe, "temp");
            start.Environment["TMP"] = start.Environment["TEMP"];
            start.Environment["HOME"] = ProbePath(probe, "home");
            start.Environment["USERPROFILE"] = start.Environment["HOME"];
            if (runtime == "python") { start.ArgumentList.Add("-I"); start.ArgumentList.Add("-S"); }
            start.ArgumentList.Add(runtime == "node" ? "-e" : "-c");
            start.ArgumentList.Add(script);
            start.ArgumentList.Add(ProbePath(probe, runtime + "-short"));
            start.ArgumentList.Add(LongProbePath(probe, runtime));
            var output = new List<string>();
            var hasErrors = false;
            var result = await _runner.RunAsync(new(start, null, TimeSpan.FromSeconds(20), 4096, 4096, 1024),
                line => { if (output.Count < 2) output.Add(line); }, _ => hasErrors = true, token);
            token.ThrowIfCancellationRequested();
            if (result.ExitCode != 0 || hasErrors || output.Count != 1) return LongPathSupport.Unknown;
            return output[0] switch
            {
                SupportedToken => LongPathSupport.Supported,
                LimitedToken => LongPathSupport.Limited,
                _ => LongPathSupport.Unknown
            };
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return LongPathSupport.Unknown; }
        catch (Exception error) when (IsDiagnosticFailure(error)) { return LongPathSupport.Unknown; }
    }

    private bool IsPrivateInstallation(SkillRuntimeInstallation installation, string runtime)
    {
        var root = Path.GetFullPath(installation.RootDirectory);
        var executable = Path.GetFullPath(installation.EntryPoint);
        if (!Path.IsPathFullyQualified(installation.RootDirectory) || !Path.IsPathFullyQualified(installation.EntryPoint) ||
            !root.StartsWith(_paths.SkillRuntimeReleases + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !executable.Equals(Path.Combine(root, runtime + ".exe"), StringComparison.OrdinalIgnoreCase)) return false;
        ManagedAiPaths.RejectReparsePoints(executable);
        return File.Exists(executable);
    }

    private static string ProbePath(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ManagedAiException(AiFailureCode.InvalidPath);
        ManagedAiPaths.RejectReparsePoints(full);
        return full;
    }

    private static string LongProbePath(string root, string runtime)
    {
        var directory = ProbePath(root, runtime + "-long");
        while (Path.Combine(directory, "renamed.txt").Length < 340)
            directory = Path.Combine(directory, "segment-0123456789abcdefghijklmnop");
        ManagedAiPaths.RejectReparsePoints(directory);
        return directory;
    }

    private static bool IsDiagnosticFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        SecurityException or Win32Exception or ManagedAiException or ArgumentException or InvalidOperationException;

    private const string NodeScript = """
        const fs = require('node:fs'), path = require('node:path');
        function probe(directory) {
          fs.mkdirSync(directory, { recursive: true });
          const original = path.join(directory, 'probe.txt'), renamed = path.join(directory, 'renamed.txt');
          fs.writeFileSync(original, 'tablecloth', { flag: 'wx' });
          if (fs.readFileSync(original, 'utf8') !== 'tablecloth') throw new Error('read');
          fs.renameSync(original, renamed);
          if (fs.readFileSync(renamed, 'utf8') !== 'tablecloth') throw new Error('read');
          fs.unlinkSync(renamed);
        }
        let result = 'TABLECLOTH_LONGPATH_UNKNOWN';
        try {
          probe(process.argv[1]);
          try { probe(process.argv[2]); result = 'TABLECLOTH_LONGPATH_SUPPORTED'; }
          catch (error) {
            if (error.code === 'ENAMETOOLONG' || error.winerror === 206 || error.win32Error === 206)
              result = 'TABLECLOTH_LONGPATH_LIMITED';
          }
        } catch { }
        console.log(result);
        """;

    private const string PythonScript = """
        import errno, os, sys
        def probe(directory):
            os.makedirs(directory, exist_ok=True)
            original = os.path.join(directory, 'probe.txt')
            renamed = os.path.join(directory, 'renamed.txt')
            with open(original, 'x', encoding='utf-8') as output:
                output.write('tablecloth')
            with open(original, encoding='utf-8') as source:
                if source.read() != 'tablecloth': raise OSError('read')
            os.rename(original, renamed)
            with open(renamed, encoding='utf-8') as source:
                if source.read() != 'tablecloth': raise OSError('read')
            os.remove(renamed)
        result = 'TABLECLOTH_LONGPATH_UNKNOWN'
        try:
            probe(sys.argv[1])
            try:
                probe(sys.argv[2])
                result = 'TABLECLOTH_LONGPATH_SUPPORTED'
            except OSError as error:
                winerror = getattr(error, 'winerror', None)
                if winerror == 206 or error.errno == errno.ENAMETOOLONG:
                    result = 'TABLECLOTH_LONGPATH_LIMITED'
                elif winerror in (3, 123):
                    try:
                        probe('\\\\?\\' + sys.argv[2] + '-extended')
                        result = 'TABLECLOTH_LONGPATH_LIMITED'
                    except Exception:
                        pass
        except Exception:
            pass
        print(result)
        """;
}

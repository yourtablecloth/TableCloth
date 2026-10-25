using System.Diagnostics;
using System.Text;

namespace TableCloth.ManagedAi.OpenAi;

/// <summary>Connects app-owned language runtimes without changing the host environment.</summary>
public static class SkillRuntimeProcessEnvironment
{
    public static void Apply(ProcessStartInfo process, ManagedAiPaths paths, SkillRuntimeEnvironment runtimes)
    {
        ValidateRuntime(paths, runtimes.NodeJs, "node.exe");
        ValidateRuntime(paths, runtimes.Python, "python.exe");
        var home = DirectoryUnder(paths, "home");
        var temporary = DirectoryUnder(paths, "temp");
        var cache = DirectoryUnder(paths, "npm-cache");
        var prefix = DirectoryUnder(paths, "npm-prefix");
        var scripts = DirectoryUnder(paths, "bin");
        var pythonUser = DirectoryUnder(paths, "python-user");
        var roaming = DirectoryUnder(paths, "home", "AppData", "Roaming");
        var local = DirectoryUnder(paths, "home", "AppData", "Local");
        var userConfig = paths.Under("profiles", "skill-tools", "npm-user.npmrc");
        var globalConfig = paths.Under("profiles", "skill-tools", "npm-global.npmrc");
        WriteOwned(userConfig, "update-notifier=false\nfund=false\naudit=false\n");
        WriteOwned(globalConfig, "# Managed by TableCloth.\n");
        // .cmd launchers avoid PowerShell's selection of the upstream npm.ps1/npx.ps1 files.
        foreach (var command in new[] { "npm", "npx" })
        {
            var cli = Path.Combine(runtimes.NodeJs.RootDirectory, "node_modules", "npm", "bin", command + "-cli.js");
            ManagedAiPaths.RejectReparsePoints(cli);
            if (!File.Exists(cli)) throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
            WriteOwned(Path.Combine(scripts, command + ".cmd"), "@echo off\r\n@setlocal DisableDelayedExpansion\r\n@\"" +
                "%~dp0" + BatchPath(Path.GetRelativePath(scripts, runtimes.NodeJs.EntryPoint)) + "\" \"%~dp0" +
                BatchPath(Path.GetRelativePath(scripts, cli)) + "\" %*\r\n");
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(windows)) throw new ManagedAiException(AiFailureCode.InvalidPath);
        var system = Path.Combine(windows, "System32");
        var environment = process.Environment;
        string[] retained = ["SystemRoot", "WINDIR", "ComSpec", "SystemDrive", "TEMP", "TMP", "USERPROFILE",
            "LOCALAPPDATA", "APPDATA", "HOMEDRIVE", "HOMEPATH", "ProgramFiles", "ProgramFiles(x86)",
            "ProgramW6432", "PATHEXT", "HTTPS_PROXY", "HTTP_PROXY", "NO_PROXY", "CODEX_HOME"];
        foreach (var key in environment.Keys.Where(key => !retained.Contains(key, StringComparer.OrdinalIgnoreCase)).ToArray())
            environment.Remove(key);
        // Do not append the host PATH: it can contain global Python aliases and package executables.
        environment["PATH"] = string.Join(Path.PathSeparator, scripts, runtimes.NodeJs.RootDirectory,
            runtimes.Python.RootDirectory, prefix, system, Path.Combine(system, "WindowsPowerShell", "v1.0"),
            Path.Combine(system, "Wbem"));
        environment["PATHEXT"] = ".COM;.EXE;.BAT;.CMD";
        environment["SystemRoot"] = windows;
        environment["WINDIR"] = windows;
        environment["ComSpec"] = Path.Combine(system, "cmd.exe");
        environment["HOME"] = home;
        environment["USERPROFILE"] = home;
        environment["HOMEDRIVE"] = Path.GetPathRoot(home)!.TrimEnd(Path.DirectorySeparatorChar);
        environment["HOMEPATH"] = home[Path.GetPathRoot(home)!.Length..].Insert(0, Path.DirectorySeparatorChar.ToString());
        environment["APPDATA"] = roaming;
        environment["LOCALAPPDATA"] = local;
        environment["TEMP"] = temporary;
        environment["TMP"] = temporary;
        environment["NPM_CONFIG_CACHE"] = cache;
        environment["NPM_CONFIG_PREFIX"] = prefix;
        environment["NPM_CONFIG_USERCONFIG"] = userConfig;
        environment["NPM_CONFIG_GLOBALCONFIG"] = globalConfig;
        environment["NPM_CONFIG_UPDATE_NOTIFIER"] = "false";
        environment["PYTHONNOUSERSITE"] = "1";
        environment["PYTHONUSERBASE"] = pythonUser;
        environment["PYTHONDONTWRITEBYTECODE"] = "1";
        environment["PYTHONUTF8"] = "1";
        environment["KSKILL_PYTHON"] = runtimes.Python.EntryPoint;
    }

    private static string DirectoryUnder(ManagedAiPaths paths, params string[] components)
    {
        var directory = paths.Under(["profiles", "skill-tools", .. components]);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void ValidateRuntime(ManagedAiPaths paths, SkillRuntimeInstallation runtime, string executable)
    {
        var root = Path.GetFullPath(runtime.RootDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var releases = paths.Under("runtimes", "skill-tools", "releases") + Path.DirectorySeparatorChar;
        if (!root.StartsWith(releases, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.Combine(root, executable), Path.GetFullPath(runtime.EntryPoint), StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(runtime.EntryPoint)) throw new ManagedAiException(AiFailureCode.InvalidPath);
        ManagedAiPaths.RejectReparsePoints(runtime.EntryPoint);
    }

    private static string BatchPath(string path)
    {
        if (path.Any(c => c is '\r' or '\n' or '"')) throw new ManagedAiException(AiFailureCode.InvalidPath);
        return path.Replace("%", "%%", StringComparison.Ordinal);
    }

    private static void WriteOwned(string path, string contents)
    {
        ManagedAiPaths.RejectReparsePoints(path);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }
}

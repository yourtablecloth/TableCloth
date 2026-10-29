using System.Runtime.InteropServices;

namespace TableCloth.ManagedAi.Windows;

public sealed record ScriptRuntimePackage(string Name, string Version, string Target, string Sha256,
    Uri Url, string ArchivePrefix, string Executable)
{
    public bool Includes(string relativePath) => Name != "Node.js" || relativePath is "node.exe" or "LICENSE";
}

public sealed record ScriptRuntimePackages(ScriptRuntimePackage Python, ScriptRuntimePackage Node)
{
    // Verified against Python's official windows-3.14.9.json and Node's SHASUMS256.txt.
    public static ScriptRuntimePackages ForArchitecture(Architecture architecture)
    {
        var target = architecture switch
        {
            Architecture.X64 => "x64", Architecture.Arm64 => "arm64",
            _ => throw new ManagedAiException(AiFailureCode.UnsupportedArchitecture)
        };
        const string pythonVersion = "3.14.9";
        const string nodeVersion = "24.22.1";
        var pythonHash = architecture == Architecture.X64
            ? "f0c8bd86db7ce6336a9686f6fd0c3b71a5a2fac57d1dddc5a8d447e4688ab2dc"
            : "b9b992bc65259d44aab0fbca712b711e53ef11c05484789f3425aafa3e6c3c8a";
        var nodeHash = architecture == Architecture.X64
            ? "d2046d28c2c3dcd211ac5145c0bf30e2047ef9dfb306693c8c6e2eccd8b95fa5"
            : "a0c807319fede24c4a0d8f84564c82a619bd313b16b10c007c383c2609e99171";
        var pythonName = $"python-{pythonVersion}-embeddable-{(target == "x64" ? "amd64" : "arm64")}.zip";
        var nodePrefix = $"node-v{nodeVersion}-win-{target}";
        return new(new("Python", pythonVersion, target, pythonHash,
            new($"https://www.python.org/ftp/python/{pythonVersion}/{pythonName}"), "", "python.exe"),
            new("Node.js", nodeVersion, target, nodeHash,
            new($"https://nodejs.org/dist/v{nodeVersion}/{nodePrefix}.zip"), nodePrefix, "node.exe"));
    }
}

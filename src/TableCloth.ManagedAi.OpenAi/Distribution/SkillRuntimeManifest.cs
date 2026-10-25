using System.Runtime.InteropServices;

namespace TableCloth.ManagedAi.OpenAi;

public sealed record SkillRuntimePackage(string Version, string Target, string Sha256, Uri Url, string ArchivePrefix);

public sealed record SkillRuntimeManifest(SkillRuntimePackage NodeJs, SkillRuntimePackage Python)
{
    public string NpmVersion { get; init; } = "11.19.1";
    public static SkillRuntimeManifest ForArchitecture(Architecture architecture)
    {
        const string nodeVersion = "24.22.1";
        const string pythonVersion = "3.14.9";
        var target = architecture switch
        {
            Architecture.X64 => "win-x64",
            Architecture.Arm64 => "win-arm64",
            _ => throw new ManagedAiException(AiFailureCode.UnsupportedArchitecture)
        };
        var nodeHash = architecture == Architecture.X64
            ? "d2046d28c2c3dcd211ac5145c0bf30e2047ef9dfb306693c8c6e2eccd8b95fa5"
            : "a0c807319fede24c4a0d8f84564c82a619bd313b16b10c007c383c2609e99171";
        var pythonHash = architecture == Architecture.X64
            ? "3c01d7cfc666f746a6f064f60fb2156ab0ec464d3d738ab796371a46a9bb24a2"
            : "a8212664f7622ebc0cb0f9a42d7beee36cebedab97ff47c6bba668cfd85b676d";
        var prefix = $"node-v{nodeVersion}-{target}";
        return new(new(nodeVersion, target, nodeHash, new($"https://nodejs.org/dist/v{nodeVersion}/{prefix}.zip"), prefix),
            new(pythonVersion, target, pythonHash, new($"https://www.python.org/ftp/python/{pythonVersion}/python-{pythonVersion}-embed-{(architecture == Architecture.X64 ? "amd64" : "arm64")}.zip"), ""));
    }

    public void Validate()
    {
        if (NodeJs.Target != Python.Target || NodeJs.Target is not ("win-x64" or "win-arm64")) throw Invalid();
        ValidateVersion(NodeJs.Version);
        ValidateVersion(Python.Version);
        ValidateVersion(NpmVersion);
        if (!Python.Version.StartsWith("3.", StringComparison.Ordinal)) throw Invalid();
        var prefix = $"node-v{NodeJs.Version}-{NodeJs.Target}";
        if (NodeJs.ArchivePrefix != prefix || Python.ArchivePrefix.Length != 0 ||
            NodeJs.Url.OriginalString != $"https://nodejs.org/dist/v{NodeJs.Version}/{prefix}.zip" ||
            Python.Url.OriginalString != $"https://www.python.org/ftp/python/{Python.Version}/python-{Python.Version}-embed-{(Python.Target == "win-x64" ? "amd64" : "arm64")}.zip") throw Invalid();
        foreach (var package in new[] { NodeJs, Python })
            if (package.Sha256.Length != 64 || !package.Sha256.All(char.IsAsciiHexDigit) || !package.Url.IsAbsoluteUri ||
                package.Url.Scheme != "https" || !package.Url.IsDefaultPort || package.Url.UserInfo.Length != 0 ||
                package.Url.Query.Length != 0 || package.Url.Fragment.Length != 0) throw Invalid();
    }

    public string PythonStem => "python" + Python.Version.Split('.')[0] + Python.Version.Split('.')[1];

    private static void ValidateVersion(string version)
    {
        var parts = version.Split('.');
        if (version.Length > 32 || parts.Length != 3 || parts.Any(x => x.Length is 0 or > 5 || !x.All(char.IsAsciiDigit))) throw Invalid();
    }
    private static ManagedAiException Invalid() => new(AiFailureCode.ReleaseFormatUnknown);
}

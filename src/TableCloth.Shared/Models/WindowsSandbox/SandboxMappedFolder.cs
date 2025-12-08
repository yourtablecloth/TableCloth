#nullable enable

namespace TableCloth.Models.WindowsSandbox
{
    public sealed class SandboxMappedFolder
    {
        public const string DefaultAssetPath = @"C:\assets";

        public string HostFolder { get; set; } = string.Empty;

        // https://docs.microsoft.com/en-us/windows/whats-new/whats-new-windows-10-version-2004#virtualization
        public string? SandboxFolder { get; set; }

        public string? ReadOnly { get; set; }
    }
}

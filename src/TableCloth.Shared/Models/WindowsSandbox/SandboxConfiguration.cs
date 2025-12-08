#nullable enable

using System.Collections.Generic;

namespace TableCloth.Models.WindowsSandbox
{
    public sealed class SandboxConfiguration
    {
        public string? Networking { get; set; }

        public string? AudioInput { get; set; }

        public string? VideoInput { get; set; }

        public string? VirtualGpu { get; set; }

        public string? PrinterRedirection { get; set; }

        public string? ClipboardRedirection { get; set; }

        public string? ProtectedClient { get; set; }

        public List<string> LogonCommand { get; set; } = new List<string>();

        public List<SandboxMappedFolder> MappedFolders { get; } = new List<SandboxMappedFolder>();

        public int? MemoryInMB { get; set; }
    }
}

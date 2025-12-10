using System.Collections.Generic;

namespace TableCloth.Models.WindowsSandbox
{
    public sealed class SandboxConfiguration
    {
        public string Networking { get; set; } = null;

        public string AudioInput { get; set; } = null;

        public string VideoInput { get; set; } = null;

        public string VirtualGpu { get; set; } = null;

        public string PrinterRedirection { get; set; } = null;

        public string ClipboardRedirection { get; set; } = null;

        public string ProtectedClient { get; set; } = null;

        public List<string> LogonCommand { get; set; } = new List<string>();

        public List<SandboxMappedFolder> MappedFolders { get; } = new List<SandboxMappedFolder>();

        public int? MemoryInMB { get; set; }
    }
}

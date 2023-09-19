using CommandLine;
using System.IO.Compression;

namespace Loom.Options
{
    [Verb("compose", HelpText = "Write an extended Windows Sandbox package zip file.")]
    public sealed class ComposeOption
    {
        [Option('g', "virtual-gpu",
            HelpText = "Enable or disable Virtual GPU.",
            Required = false)]
        public bool? VirtualGpu { get; set; }

        [Option('n', "networking",
            HelpText = "Enable or disable networking.",
            Required = false)]
        public bool? Networking { get; set; }

        [Option('v', "video-input",
            HelpText = "Enable or disable video input (camera).",
            Required = false)]
        public bool? VideoInput { get; set; }

        [Option('a', "audio-input",
            HelpText = "Enable or disable audio input (microphone).",
            Required = false)]
        public bool? AudioInput { get; set; }

        [Option('s', "protected-client",
            HelpText = "Enable or disable protected client.",
            Required = false)]
        public bool? ProtectedClient { get; set; }

        [Option('p', "printer-redirection",
            HelpText = "Enable or disable printer redirection.",
            Required = false)]
        public bool? PrinterRedirection { get; set; }

        [Option('c', "clipboard-redirection",
            HelpText = "Enable or disable clipboard redirection.",
            Required = false)]
        public bool? ClipboardRedirection { get; set; }

        [Option('z', "memory-size",
            HelpText = "Memory in mega-byte size.",
            Required = false)]
        public int? MemorySizeInMb { get; set; }

        [Option('l', "compression-level",
            HelpText = "Compression level.",
            Required = false,
            Default = CompressionLevel.Optimal)]
        public CompressionLevel CompressionLevel { get; set; }

        [Option("startup-command",
            HelpText = "Startup inline DOS command.",
            SetName = nameof(StartupCommand),
            Required = false)]
        public string? StartupCommand { get; set; }

        [Option("startup-batch-file",
            HelpText = "Startup batch file path.",
            SetName = nameof(StartupBatchFilePath),
            Required = false)]
        public string? StartupBatchFilePath { get; set; }

        [Option('i', "include",
            HelpText = "Files to include.",
            Required = false)]
        public IEnumerable<string>? FilesToInclude { get; set; }

        [Option('f', "force",
            HelpText = "Overwrite output file.",
            Default = false,
            Required = false)]
        public bool OverwriteOutputFilePath { get; set; }

        [Option('o', "output",
            HelpText = "Output file path.",
            Required = true)]
        public string OutputFilePath { get; set; }
    }
}

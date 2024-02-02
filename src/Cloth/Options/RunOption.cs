using CommandLine;

namespace Cloth.Options;

[Verb("run", HelpText = "Run the extended Windows Sandbox package zip file.")]
public class RunOption
{
    // --map host= "Host Path" sandbox= "Sandbox Path" readonly= true / host= "Host Path" sandbox= "Sandbox Path" / host= "Host Path" readonly= true / host= "Host Path"
    [Option('m', "map",
        HelpText = "Add folder mapping between host and sandbox environment.")]
    public IEnumerable<string>? FolderMapping { get; set; }

    [Option('t', "temporary-directory",
        HelpText = "Specify temporary directory path",
        Required = false)]
    public string? TemporaryDirectoryPath { get; set; }

    [Option('e', "erase-temporary-directory",
        HelpText = "Erase temporary directory when exit.",
        Default = true,
        Required = false)]
    public bool EraseTemporaryDirectory { get; set; }

    [Option('w', "no-wait",
        HelpText = "Run sandbox and return immediately. This option will ignore -e option.",
        Default = false,
        Required = false)]
    public bool NoWait { get; set; }

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

    [Option('i', "input",
        HelpText = "Existing input wsbx file path.",
        Required = true)]
    public string InputFilePath { get; set; } = string.Empty;
}

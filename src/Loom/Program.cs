using CommandLine;
using Loom.Options;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Models.WindowsSandbox;

namespace Loom;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var types = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null)
            .ToArray();

        Parser.Default
            .ParseArguments(args, types)
            .WithParsed(Run)
            .WithNotParsed(HandleErrors);
    }

    private static void HandleErrors(IEnumerable<Error> obj)
    {
        foreach (var eachError in obj)
            Console.Out.WriteLine(eachError.ToString());
    }

    private static void Run(object obj)
    {
        switch (obj)
        {
            case ComposeOption o:
                CreateExtendedSandboxArchive(o);
                break;
            case RunOption o:
                RunExtendedSandboxArchive(o);
                break;
            default:
                break;
        }
    }

    private static void RunExtendedSandboxArchive(
        RunOption runOption)
    {
        const string Enable = "Enable";
        const string Disable = "Disable";
        const string SandboxPathPrefix = @"C:\Users\WDAGUtilityAccount\Desktop";

        var workingDirectoryPath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("n"));

        if (!string.IsNullOrWhiteSpace(runOption.TemporaryDirectoryPath))
        {
            if (!Directory.Exists(runOption.TemporaryDirectoryPath))
                Directory.CreateDirectory(runOption.TemporaryDirectoryPath);

            workingDirectoryPath = runOption.TemporaryDirectoryPath;
        }

        var comSpecPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "cmd.exe");

        var wsbFilePath = Path.Combine(workingDirectoryPath, "entry.wsb");

        var assetsDirectoryPath = Path.Combine(
            workingDirectoryPath, "assets");

        var startupFilePath = Path.Combine(assetsDirectoryPath, "startup.cmd");

        try
        {
            if (File.Exists(workingDirectoryPath))
                File.Delete(workingDirectoryPath);
            if (Directory.Exists(workingDirectoryPath))
                Directory.Delete(workingDirectoryPath);
            if (!Directory.Exists(workingDirectoryPath))
                Directory.CreateDirectory(workingDirectoryPath);

            if (!File.Exists(runOption.InputFilePath))
                throw new FileNotFoundException("WSBX file doees not existing.");

            ZipFile.ExtractToDirectory(runOption.InputFilePath, workingDirectoryPath, true);

            var serializer = new XmlSerializer(
                typeof(SandboxConfiguration),
                new Type[] { typeof(SandboxMappedFolder), });

            var utf8Encoding = new UTF8Encoding(false);

            var ns = new XmlSerializerNamespaces();
            ns.Add(string.Empty, string.Empty);

            var settings = new XmlWriterSettings()
            {
                Indent = true,
                Encoding = utf8Encoding,
            };

            var config = default(SandboxConfiguration);

            using (var reader = File.OpenText(wsbFilePath))
            {
                config = serializer.Deserialize(reader) as SandboxConfiguration;
            }

            if (config == null)
            {
                Console.Error.WriteLine("Cannot load sandbox specification XML file from ZIP archive.");
                return;
            }

            config.MappedFolders.Add(new SandboxMappedFolder()
            {
                HostFolder = assetsDirectoryPath,
            });

            if (File.Exists(startupFilePath))
            {
                config.LogonCommand.Add(
                    Path.Combine(SandboxPathPrefix, Path.GetFileName(assetsDirectoryPath), "startup.cmd"));
            }

            if (runOption.AudioInput.HasValue)
                config.AudioInput = runOption.AudioInput.Value ? Enable : Disable;

            if (runOption.VideoInput.HasValue)
                config.VideoInput = runOption.VideoInput.Value ? Enable : Disable;

            if (runOption.VirtualGpu.HasValue)
                config.VirtualGpu = runOption.VirtualGpu.Value ? Enable : Disable;

            if (runOption.PrinterRedirection.HasValue)
                config.PrinterRedirection = runOption.PrinterRedirection.Value ? Enable : Disable;

            if (runOption.ClipboardRedirection.HasValue)
                config.ClipboardRedirection = runOption.ClipboardRedirection.Value ? Enable : Disable;

            if (runOption.ProtectedClient.HasValue)
                config.ProtectedClient = runOption.ProtectedClient.Value ? Enable : Disable;

            if (runOption.Networking.HasValue)
                config.Networking = runOption.Networking.Value ? Enable : Disable;

            if (runOption.MemorySizeInMb.HasValue)
                config.MemoryInMB = runOption.MemorySizeInMb.Value;

            if (runOption.FolderMapping != null)
                config.MappedFolders.AddRange(ParseFolderMappingArguments(runOption.FolderMapping));

            var memStream = new MemoryStream();

            using (var xmlWriter = XmlWriter.Create(memStream, settings))
            {
                serializer.Serialize(xmlWriter, config, ns);
            }

            var content = utf8Encoding.GetString(memStream.ToArray());
            File.WriteAllText(wsbFilePath, content, utf8Encoding);

            var process = new Process()
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo(comSpecPath, "/c start /wait \"\" \"" + wsbFilePath + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            if (!process.Start())
            {
                process.Dispose();
                //_appMessageBox.DisplayError(StringResources.Error_Windows_Sandbox_CanNotStart, true);
            }

            if (!runOption.NoWait)
                process.WaitForExit();
        }
        finally
        {
            if (!runOption.NoWait &&
                Directory.Exists(workingDirectoryPath) &&
                runOption.EraseTemporaryDirectory)
            {
                try { Directory.Delete(workingDirectoryPath, true); }
                catch { /* Ignore error */ }
            }
        }
    }

    private static void CreateExtendedSandboxArchive(
        ComposeOption composeOption)
    {
        const string Enable = "Enable";
        const string Disable = "Disable";

        var workingDirectoryPath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("n"));

        if (File.Exists(workingDirectoryPath))
            File.Delete(workingDirectoryPath);
        if (Directory.Exists(workingDirectoryPath))
            Directory.Delete(workingDirectoryPath);
        if (!Directory.Exists(workingDirectoryPath))
            Directory.CreateDirectory(workingDirectoryPath);

        var assetsDirectoryPath = Path.Combine(
            workingDirectoryPath, "assets");

        if (!Directory.Exists(assetsDirectoryPath))
            Directory.CreateDirectory(assetsDirectoryPath);

        var config = new SandboxConfiguration();

        if (composeOption.AudioInput.HasValue)
            config.AudioInput = composeOption.AudioInput.Value ? Enable : Disable;

        if (composeOption.VideoInput.HasValue)
            config.VideoInput = composeOption.VideoInput.Value ? Enable : Disable;

        if (composeOption.VirtualGpu.HasValue)
            config.VirtualGpu = composeOption.VirtualGpu.Value ? Enable : Disable;

        if (composeOption.PrinterRedirection.HasValue)
            config.PrinterRedirection = composeOption.PrinterRedirection.Value ? Enable : Disable;

        if (composeOption.ClipboardRedirection.HasValue)
            config.ClipboardRedirection = composeOption.ClipboardRedirection.Value ? Enable : Disable;

        if (composeOption.ProtectedClient.HasValue)
            config.ProtectedClient = composeOption.ProtectedClient.Value ? Enable : Disable;

        if (composeOption.Networking.HasValue)
            config.Networking = composeOption.Networking.Value ? Enable : Disable;

        if (composeOption.MemorySizeInMb.HasValue)
            config.MemoryInMB = composeOption.MemorySizeInMb.Value;

        if (!string.IsNullOrWhiteSpace(composeOption.StartupCommand))
            config.LogonCommand.Add(composeOption.StartupCommand);

        if (File.Exists(composeOption.StartupBatchFilePath))
        {
            File.Copy(
                composeOption.StartupBatchFilePath,
                Path.Combine(assetsDirectoryPath, "startup.cmd"),
                true);
        }

        if (composeOption.FilesToInclude != null)
        {
            foreach (var eachFileToInclude in composeOption.FilesToInclude)
            {
                File.Copy(
                    eachFileToInclude,
                    Path.Combine(assetsDirectoryPath, Path.GetFileName(eachFileToInclude)),
                    true);
            }
        }

        var serializer = new XmlSerializer(
            typeof(SandboxConfiguration),
            new Type[] { typeof(SandboxMappedFolder), });

        var utf8Encoding = new UTF8Encoding(false);

        var ns = new XmlSerializerNamespaces();
        ns.Add(string.Empty, string.Empty);

        var settings = new XmlWriterSettings()
        {
            Indent = true,
            Encoding = utf8Encoding,
        };

        var memStream = new MemoryStream();

        using (var xmlWriter = XmlWriter.Create(memStream, settings))
        {
            serializer.Serialize(xmlWriter, config, ns);
        }

        var content = utf8Encoding.GetString(memStream.ToArray());
        File.WriteAllText(
            Path.Combine(workingDirectoryPath, "entry.wsb"),
            content, utf8Encoding);

        if (File.Exists(composeOption.OutputFilePath))
        {
            if (composeOption.OverwriteOutputFilePath)
                File.Delete(composeOption.OutputFilePath);
            else
                throw new Exception("Cannot overwrite existing file. Please specify overwrite option to proceed.");
        }

        ZipFile.CreateFromDirectory(
            workingDirectoryPath,
            composeOption.OutputFilePath,
            composeOption.CompressionLevel,
            false);
    }

    private static IEnumerable<SandboxMappedFolder> ParseFolderMappingArguments(IEnumerable<string> parts)
    {
        var results = new List<SandboxMappedFolder>();
        var splitted = new List<List<string>>();
        var list = new List<string>();

        foreach (var eachFragment in parts)
        {
            if (string.Equals(eachFragment, "/", StringComparison.Ordinal))
            {
                splitted.Add(list);
                list = new List<string>();
                continue;
            }

            list.Add(eachFragment);
        }
        splitted.Add(list);

        foreach (var eachParts in splitted)
        {
            string? hostPath = null, sandboxPath = null, mapAsReadOnly = null;

            foreach (var eachChunk in eachParts.Chunk(2))
            {
                if (eachChunk.Count() < 2)
                    continue;

                switch (eachChunk.ElementAtOrDefault(0))
                {
                    case "host=":
                        hostPath = eachChunk.ElementAtOrDefault(1);
                        break;
                    case "sandbox=":
                        sandboxPath = eachChunk.ElementAtOrDefault(1);
                        break;
                    case "readonly=":
                        mapAsReadOnly = eachChunk.ElementAtOrDefault(1);
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(hostPath))
                continue;

            if (bool.TryParse(mapAsReadOnly, out bool result))
                mapAsReadOnly = result.ToString().ToLowerInvariant();
            else
                mapAsReadOnly = null;

            results.Add(new SandboxMappedFolder()
            {
                HostFolder = hostPath,
                SandboxFolder = sandboxPath,
                ReadOnly = mapAsReadOnly,
            });
        }

        return results;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Contracts;
using TableCloth.Implementations.WindowsSandbox;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class SandboxBuilder : ISandboxBuilder
    {
        private readonly bool _isSandboxLocalPathSupported =
            Environment.OSVersion.Version >= new Version(10, 0, 19041);

        private readonly string _wdagUtilityAccountPath = @"C:\Users\WDAGUtilityAccount";

        private string GetAssetsPathForSandbox()
            => _isSandboxLocalPathSupported ? @"C:\Assets" : Path.Combine(_wdagUtilityAccountPath, "Desktop", "Assets");

        private string GetNPKIPathForSandbox(X509CertPair certPair)
        {
            var candidatePath = Path.Join("AppData", "LocalLow", "NPKI", certPair.SubjectOrganization);

            if (certPair.IsPersonalCert)
                candidatePath = Path.Join(candidatePath, "USER", certPair.SubjectNameForNpkiApp);

            return Path.Join(_wdagUtilityAccountPath, candidatePath);
        }

        public string GenerateSandboxConfiguration(string outputDirectory, TableClothConfiguration tableClothConfiguration, IList<SandboxMappedFolder> excludedDirectories)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

            var assembly = typeof(SandboxBuilder).Assembly;
            var hostessZipFileKey = assembly.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith("Hostess.zip", StringComparison.OrdinalIgnoreCase));
            using var hostessZipFileStream = assembly.GetManifestResourceStream(hostessZipFileKey);
            ExpandHostessFiles(hostessZipFileStream, outputDirectory);

            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var assetsDirectory = Path.Combine(outputDirectory, "assets");
            if (!Directory.Exists(assetsDirectory))
                Directory.CreateDirectory(assetsDirectory);

            var signatureImageContent = GraphicResources.SignatureJpegImage;
            var signatureFilePath = Path.Combine(assetsDirectory, "Signature.jpg");
            File.WriteAllBytes(signatureFilePath, Convert.FromBase64String(signatureImageContent));

            var bootstrapFileContent = GenerateSandboxBootstrapPowerShellScript(tableClothConfiguration);
            var bootstrapFilePath = Path.Combine(assetsDirectory, "Bootstrap.ps1");
            File.WriteAllText(bootstrapFilePath, bootstrapFileContent, Encoding.Unicode);

            var batchFileContent = GenerateSandboxStartupScript(tableClothConfiguration);
            var batchFilePath = Path.Combine(assetsDirectory, "StartupScript.cmd");
            File.WriteAllText(batchFilePath, batchFileContent, Encoding.Default);

            tableClothConfiguration.AssetsDirectoryPath = assetsDirectory;

            var wsbFilePath = Path.Combine(outputDirectory, "InternetBankingSandbox.wsb");
            var serializedXml = SerializeSandboxSpec(BootstrapSandboxConfiguration(tableClothConfiguration), excludedDirectories);
            File.WriteAllText(wsbFilePath, serializedXml);

            return wsbFilePath;
        }

        private string GenerateSandboxBootstrapPowerShellScript(TableClothConfiguration tableClothConfiguration)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

            var buffer = new StringBuilder();

            buffer = buffer.AppendLine($@"
# Change Wallpaper
$SetWallpaperSource = @""
using System.Runtime.InteropServices;
using System.Threading;

public static class Wallpaper {{
  public const int SetDesktopWallpaper = 0x0014;
  public const int UpdateIniFile = 0x01;
  public const int SendWinIniChange = 0x02;

  [DllImport(""user32.dll"", SetLastError = true, CharSet = CharSet.Auto)]
  private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

  public static void SetWallpaper(string path) {{
    SystemParametersInfo(SetDesktopWallpaper, 0, path, UpdateIniFile | SendWinIniChange);
  }}
}}
""@
Add-Type -TypeDefinition $SetWallpaperSource

$WallpaperPath = ""{Path.Combine(GetAssetsPathForSandbox(), "Signature.jpg")}""
[Wallpaper]::SetWallpaper($WallpaperPath)
rundll32.exe user32.dll,UpdatePerUserSystemParameters 1, True
");

            if (_isSandboxLocalPathSupported)
                buffer = buffer.AppendLine($@"{Path.Combine(GetAssetsPathForSandbox(), "Hostess.exe")} {string.Join(" ", tableClothConfiguration.Packages.Select(x => x.Id))}");
            else
            {
                if (tableClothConfiguration.CertPair != null)
                {
                    var candidatePath = GetNPKIPathForSandbox(tableClothConfiguration.CertPair);

                    buffer = buffer.AppendLine($@"
# Copy certs directory to AppData/LocalLow/NPKI path
if (-Not (Test-Path -Path ""{candidatePath}"" -Type Container)) {{ mkdir ""{candidatePath}"" | Out-Null }}
copy -Path ""{Path.Combine(GetAssetsPathForSandbox(), "certs", "*.*")}"" -Destination ""{candidatePath}"" -Force");
                }

                buffer = buffer.AppendLine($@"
# Run Hostess
. '{Path.Combine(GetAssetsPathForSandbox(), "Hostess.exe")}' {string.Join(" ", tableClothConfiguration.Packages.Select(x => x.Id))}
");
            }

            return buffer.ToString();
        }

        private SandboxConfiguration BootstrapSandboxConfiguration(TableClothConfiguration tableClothConfig)
        {
            const string Enable = "Enable";
            const string Disable = "Disable";

            var sandboxConfig = new SandboxConfiguration
            {
                AudioInput = tableClothConfig.EnableMicrophone ? Enable : Disable,
                VideoInput = tableClothConfig.EnableWebCam ? Enable : Disable,
                PrinterRedirection = tableClothConfig.EnablePrinters ? Enable : Disable,
            };

            if (!Directory.Exists(tableClothConfig.AssetsDirectoryPath))
                return sandboxConfig;

            sandboxConfig.MappedFolders.Clear();

            sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
            {
                HostFolder = tableClothConfig.AssetsDirectoryPath,
                SandboxFolder = _isSandboxLocalPathSupported ? SandboxMappedFolder.DefaultAssetPath : null,
                ReadOnly = bool.FalseString,
            });

            if (tableClothConfig.CertPair == null)
                return sandboxConfig;

            var certAssetsDirectoryPath = Path.Combine(tableClothConfig.AssetsDirectoryPath, "certs");
            if (!Directory.Exists(certAssetsDirectoryPath))
                Directory.CreateDirectory(certAssetsDirectoryPath);

            var destDerFilePath = Path.Combine(
                certAssetsDirectoryPath,
                Path.GetFileName(tableClothConfig.CertPair.DerFilePath));

            var destKeyFileName = Path.Combine(
                certAssetsDirectoryPath,
                Path.GetFileName(tableClothConfig.CertPair.KeyFilePath));

            File.Copy(tableClothConfig.CertPair.DerFilePath, destDerFilePath, true);
            File.Copy(tableClothConfig.CertPair.KeyFilePath, destKeyFileName, true);

            var candidatePath = GetNPKIPathForSandbox(tableClothConfig.CertPair);

            if (_isSandboxLocalPathSupported)
            {
                sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
                {
                    HostFolder = certAssetsDirectoryPath,
                    SandboxFolder = _isSandboxLocalPathSupported ? candidatePath : null,
                    ReadOnly = bool.TrueString,
                });
            }

            sandboxConfig.LogonCommand.Add("C:\\Windows\\System32\\cmd.exe /c " + Path.Combine(GetAssetsPathForSandbox(), "StartupScript.cmd"));
            return sandboxConfig;
        }

        private string GenerateSandboxStartupScript(TableClothConfiguration tableClothConfiguration)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

            var buffer = new StringBuilder();
            buffer = buffer.AppendLine("@echo off");
            buffer = buffer.AppendLine(@"pushd ""%~dp0""");
            buffer = buffer.AppendLine(@"powershell.exe -Command ""&{{Set-ExecutionPolicy RemoteSigned -Force}}""");
            buffer = buffer.AppendLine($@"powershell.exe -ExecutionPolicy Bypass -File ""{Path.Combine(GetAssetsPathForSandbox(), "Bootstrap.ps1")}""");
            buffer = buffer.AppendLine(@":exit");
            buffer = buffer.AppendLine(@"@popd");
            buffer = buffer.AppendLine("@echo on");
            return buffer.ToString();
        }

        private void ExpandHostessFiles(Stream hostessZipFileStream, string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var assetsDirectory = Path.Combine(outputDirectory, "assets");
            if (!Directory.Exists(assetsDirectory))
                Directory.CreateDirectory(assetsDirectory);

            using var hostessZipArchive = new ZipArchive(hostessZipFileStream, ZipArchiveMode.Read);
            hostessZipArchive.ExtractToDirectory(assetsDirectory, true);
        }

        private string SerializeSandboxSpec(SandboxConfiguration configuration, IList<SandboxMappedFolder> excludedFolders)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var unavailableDirectories = configuration.MappedFolders
                .Where(x => !Directory.Exists(x.HostFolder));

            configuration.MappedFolders.RemoveAll(x => unavailableDirectories.Contains(x));

            if (excludedFolders != null)
                foreach (var eachDirectory in unavailableDirectories)
                    excludedFolders.Add(eachDirectory);

            var serializer = new XmlSerializer(typeof(SandboxConfiguration));
            var @namespace = new XmlSerializerNamespaces(new[] { new XmlQualifiedName(string.Empty) });
            var targetEncoding = new UTF8Encoding(false);

            using var memStream = new MemoryStream();
            var contentStream = new StreamWriter(memStream);
            serializer.Serialize(contentStream, configuration, @namespace);
            return targetEncoding.GetString(memStream.ToArray());
        }
    }
}

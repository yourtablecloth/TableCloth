using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Helpers;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;

namespace TableCloth
{
    public static class SandboxBuilder
    {
        public static SandboxConfiguration BootstrapSandboxConfiguration(TableClothConfiguration tableClothConfig)
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
                SandboxFolder = SandboxMappedFolder.DefaultAssetPath,
                ReadOnly = bool.TrueString,
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

            var candidatePath = Path.Join("AppData", "LocalLow", "NPKI", tableClothConfig.CertPair.SubjectOrganization);

            if (tableClothConfig.CertPair.IsPersonalCert)
                candidatePath = Path.Join(candidatePath, "USER", tableClothConfig.CertPair.SubjectNameForNpkiApp);

            candidatePath = Path.Join(@"C:\Users\WDAGUtilityAccount", candidatePath);

            sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
            {
                HostFolder = certAssetsDirectoryPath,
                SandboxFolder = candidatePath,
                ReadOnly = bool.FalseString,
            });

            sandboxConfig.LogonCommand.Add(@"C:\assets\StartupScript.cmd");
            return sandboxConfig;
        }

        public static void ExpandCompanionFiles(string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var assetsDirectory = Path.Combine(outputDirectory, "assets");
            if (!Directory.Exists(assetsDirectory))
                Directory.CreateDirectory(assetsDirectory);

            var assembly = typeof(SandboxBuilder).Assembly;
            var hostessZipFileKey = assembly.GetManifestResourceNames().FirstOrDefault(x => x.EndsWith("Hostess.zip", StringComparison.OrdinalIgnoreCase));
            using var hostessZipFileStream = assembly.GetManifestResourceStream(hostessZipFileKey);
            using var hostessZipArchive = new ZipArchive(hostessZipFileStream, ZipArchiveMode.Read);
            hostessZipArchive.ExtractToDirectory(assetsDirectory, true);
        }

        public static string GenerateSandboxConfiguration(string outputDirectory, TableClothConfiguration tableClothConfiguration)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

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
            var serializedXml = XmlHelpers.SerializeToXml(BootstrapSandboxConfiguration(tableClothConfiguration));
            File.WriteAllText(wsbFilePath, serializedXml);

            return wsbFilePath;
        }

        public static string GenerateSandboxBootstrapPowerShellScript(TableClothConfiguration tableClothConfiguration)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

            var buffer = new StringBuilder();

            buffer.AppendLine($@"
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

$WallpaperPath = ""C:\assets\Signature.jpg""
[Wallpaper]::SetWallpaper($WallpaperPath)
rundll32.exe user32.dll,UpdatePerUserSystemParameters 1, True
");

            buffer.AppendLine($@"C:\assets\Hostess.exe {string.Join(" ", tableClothConfiguration.Packages.Select(x => x.Id))}");

            return buffer.ToString();
        }

        public static string GenerateSandboxStartupScript(TableClothConfiguration tableClothConfiguration)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

            var buffer = new StringBuilder();
            buffer.AppendLine(@"powershell.exe -Command ""&{{Set-ExecutionPolicy RemoteSigned -Force}}""");
            buffer.AppendLine(@"powershell.exe -ExecutionPolicy Bypass -File ""C:\assets\Bootstrap.ps1""");
            return buffer.ToString();
        }
    }
}

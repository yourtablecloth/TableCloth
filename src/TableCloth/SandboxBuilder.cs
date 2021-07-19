using System;
using System.IO;
using System.Linq;
using System.Text;
using TableCloth.Helpers;
using TableCloth.Internals;
using TableCloth.Models;
using TableCloth.Resources;

namespace TableCloth
{
    public static class SandboxBuilder
    {
        public static WindowsSandboxConfiguration BootstrapSandboxConfiguration(TableClothConfiguration tableClothConfig)
        {
            const string Enable = "Enable";
            const string Disable = "Disable";

            var sandboxConfig = new WindowsSandboxConfiguration
            {
                AudioInput = tableClothConfig.EnableMicrophone ? Enable : Disable,
                VideoInput = tableClothConfig.EnableWebCam ? Enable : Disable,
                PrinterRedirection = tableClothConfig.EnablePrinters ? Enable : Disable,
            };

            if (!Directory.Exists(tableClothConfig.AssetsDirectoryPath))
                return sandboxConfig;

            sandboxConfig.MappedFolders.Clear();

            sandboxConfig.MappedFolders.Add(new WindowsSandboxMappedFolder
            {
                HostFolder = tableClothConfig.AssetsDirectoryPath,
                SandboxFolder = WindowsSandboxMappedFolder.DefaultAssetPath,
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

            sandboxConfig.MappedFolders.Add(new WindowsSandboxMappedFolder
            {
                HostFolder = certAssetsDirectoryPath,
                SandboxFolder = candidatePath,
                ReadOnly = bool.FalseString,
            });

            sandboxConfig.LogonCommand.Add(@"C:\assets\StartupScript.cmd");
            return sandboxConfig;
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

            var batchFileContent = GenerateSandboxStartupScript(tableClothConfiguration);
            var batchFilePath = Path.Combine(assetsDirectory, "StartupScript.cmd");
            File.WriteAllText(batchFilePath, batchFileContent, Encoding.Default);

            tableClothConfiguration.AssetsDirectoryPath = assetsDirectory;

            var wsbFilePath = Path.Combine(outputDirectory, "InternetBankingSandbox.wsb");
            var serializedXml = XmlHelpers.SerializeToXml(BootstrapSandboxConfiguration(tableClothConfiguration));
            File.WriteAllText(wsbFilePath, serializedXml);

            return wsbFilePath;
        }

        public static string GenerateSandboxStartupScript(TableClothConfiguration tableClothConfiguration)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

            var buffer = new StringBuilder();
            var services = tableClothConfiguration.Packages;

            var infoMessage = StringResources.Script_InstructionMessage(
                services.Sum(x => x.Packages.Count()),
                string.Join(", ", services.Select(x => x.DisplayName)));
            var value = $@"PowerShell -Command ""Add-Type -AssemblyName System.Windows.Forms;[System.Windows.Forms.MessageBox]::Show('{infoMessage}', '안내', [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information)""";
            buffer.AppendLine(value);

            foreach (var eachPackage in services.SelectMany(service => service.Packages))
            {
                var localFileName = GetLocalFileName(eachPackage.Url.LocalPath);

                buffer
                    .AppendLine($@"REM Run {eachPackage.Name} Setup")
                    .AppendLine($@"curl -L ""{eachPackage.Url}"" --output ""%temp%\{localFileName}""")
                    .AppendLine(!string.IsNullOrWhiteSpace(eachPackage.Arguments)
                        ? $@"start /abovenormal /wait %temp%\{localFileName} {eachPackage.Arguments}"
                        : $@"start /abovenormal /wait %temp%\{localFileName}")
                    .AppendLine();
            }

            foreach (var eachHomePageUrl in services.Select(x => x.Url.AbsoluteUri))
                buffer.AppendLine($@"start /max {eachHomePageUrl}");

            return buffer.ToString();

            static string GetLocalFileName(string localPath)
            {
                try { return Path.GetFileName(localPath); }
                catch { return $"{Guid.NewGuid():N}.exe"; }
            }
        }
    }
}

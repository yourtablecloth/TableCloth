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

namespace TableCloth.Implementations
{
    public sealed class SandboxBuilder : ISandboxBuilder
    {
        private readonly string _wdagUtilityAccountPath = @"C:\Users\WDAGUtilityAccount";

        private string GetAssetsPathForSandbox()
            => Path.Combine(_wdagUtilityAccountPath, "Desktop", "Assets");

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

            var batchFileContent = GenerateSandboxStartupScript(tableClothConfiguration);
            var batchFilePath = Path.Combine(assetsDirectory, "StartupScript.cmd");
            File.WriteAllText(batchFilePath, batchFileContent, Encoding.Default);

            tableClothConfiguration.AssetsDirectoryPath = assetsDirectory;

            var wsbFilePath = Path.Combine(outputDirectory, "InternetBankingSandbox.wsb");
            var serializedXml = SerializeSandboxSpec(BootstrapSandboxConfiguration(tableClothConfiguration), excludedDirectories);
            File.WriteAllText(wsbFilePath, serializedXml);

            return wsbFilePath;
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
                SandboxFolder = null,
                ReadOnly = bool.FalseString,
            });

            if (tableClothConfig.CertPair != null)
            {
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

                sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
                {
                    HostFolder = certAssetsDirectoryPath,
                    SandboxFolder = null,
                    ReadOnly = bool.TrueString,
                });
            }

            sandboxConfig.LogonCommand.Add(Path.Combine(GetAssetsPathForSandbox(), "StartupScript.cmd"));
            return sandboxConfig;
        }

        private string GenerateSandboxStartupScript(TableClothConfiguration tableClothConfiguration)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

            var certFileCopyScript = string.Empty;

            if (tableClothConfiguration.CertPair != null)
            {
                var npkiDirectoryPath = GetNPKIPathForSandbox(tableClothConfiguration.CertPair);
                var providedCertFilePath = Path.Combine(GetAssetsPathForSandbox(), "certs", "*.*");
                certFileCopyScript = $@"
if not exist ""{npkiDirectoryPath}"" mkdir ""{npkiDirectoryPath}""
copy /y ""{providedCertFilePath}"" ""{npkiDirectoryPath}""
";
            }

            var everyonesPrinterSetupScript = string.Empty;

            if (tableClothConfiguration.EnableEveryonesPrinter)
            {
                var everyonesPrinterElement = tableClothConfiguration.Companions
                    .Where(x => string.Equals(x.Id, "EveryonesPrinter", StringComparison.Ordinal))
                    .SingleOrDefault();

                if (everyonesPrinterElement != null)
                {
                    var downloadUrl = everyonesPrinterElement.Url.Replace("?", "^?").Replace("&", "^&");
                    everyonesPrinterSetupScript = $@"
curl.exe -L ""{downloadUrl}"" -o ""%temp%\MopInstaller.exe""
""%temp%\MopInstaller.exe""
";
                }
            }

            var hostessFilePath = Path.Combine(GetAssetsPathForSandbox(), "Hostess.exe");
            var idList = string.Join(" ", tableClothConfiguration.Services.Select(x => x.Id).Distinct());

            return $@"@echo off
pushd ""%~dp0""
{certFileCopyScript}
""{hostessFilePath}"" {idList}
{everyonesPrinterSetupScript}
:exit
popd
@echo on
";
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

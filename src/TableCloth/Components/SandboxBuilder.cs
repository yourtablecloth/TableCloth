using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;

namespace TableCloth.Components
{
    public sealed class SandboxBuilder
    {
        public SandboxBuilder(SharedLocations sharedLocations)
        {
            this._sharedLocations = sharedLocations;
        }

        private readonly SharedLocations _sharedLocations;

        private readonly string _wdagUtilityAccountPath = @"C:\Users\WDAGUtilityAccount";

        private string GetAssetsPathForSandbox()
            => Path.Combine(_wdagUtilityAccountPath, "Desktop", "Assets");

        private string GetNPKIPathForSandbox(X509CertPair certPair)
        {
            var candidatePath = Path.Join("AppData", "LocalLow", "NPKI", certPair.Organization);

            if (certPair.IsPersonalCert)
                candidatePath = Path.Join(candidatePath, "USER", certPair.SubjectNameForNpkiApp);

            return Path.Join(_wdagUtilityAccountPath, candidatePath);
        }

        public string GenerateSandboxConfiguration(string outputDirectory, TableClothConfiguration tableClothConfiguration, IList<SandboxMappedFolder> excludedDirectories)
        {
            if (tableClothConfiguration == null)
                throw new ArgumentNullException(nameof(tableClothConfiguration));

            using var hostessZipFileStream = File.OpenRead(
                Path.Combine(_sharedLocations.ExecutableDirectoryPath, "Hostess.zip"));
            ExpandAssetZip(hostessZipFileStream, outputDirectory);

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

                var destDerFilePath = Path.Combine(certAssetsDirectoryPath, "signCert.der");
                var destKeyFileName = Path.Combine(certAssetsDirectoryPath, "signPri.key");

                File.WriteAllBytes(destDerFilePath, tableClothConfig.CertPair.PublicKey);
                File.WriteAllBytes(destKeyFileName, tableClothConfig.CertPair.PrivateKey);

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
                var npkiDirectoryPathInSandbox = GetNPKIPathForSandbox(tableClothConfiguration.CertPair);
                var desktopDirectoryPathInSandbox = "%userprofile%\\Desktop\\Certificates";
                var providedCertFilePath = Path.Combine(GetAssetsPathForSandbox(), "certs", "*.*");
                certFileCopyScript = $@"
if not exist ""{npkiDirectoryPathInSandbox}"" mkdir ""{npkiDirectoryPathInSandbox}""
copy /y ""{providedCertFilePath}"" ""{npkiDirectoryPathInSandbox}""
if not exist ""{desktopDirectoryPathInSandbox}"" mkdir ""{desktopDirectoryPathInSandbox}""
copy /y ""{providedCertFilePath}"" ""{desktopDirectoryPathInSandbox}""
del /f /q ""{providedCertFilePath}""
";
            }

            var switches = new List<string>();

            if (tableClothConfiguration.EnableEveryonesPrinter)
                switches.Add(StringResources.TableCloth_Switch_EnableEveryonesPrinter);

            if (tableClothConfiguration.EnableAdobeReader)
                switches.Add(StringResources.TableCloth_Switch_EnableAdobeReader);

            if (tableClothConfiguration.EnableHancomOfficeViewer)
                switches.Add(StringResources.TableCloth_Switch_EnableHancomOfficeViewer);

            if (tableClothConfiguration.EnableRaiDrive)
                switches.Add(StringResources.TableCloth_Switch_EnableRaiDrive);

            if (tableClothConfiguration.EnableInternetExplorerMode)
                switches.Add(StringResources.TableCloth_Switch_EnableIEMode);

            var hostessFilePath = Path.Combine(GetAssetsPathForSandbox(), "Hostess.exe");
            var idList = string.Join(" ", tableClothConfiguration.Services.Select(x => x.Id).Distinct());

            return $@"@echo off
pushd ""%~dp0""
{certFileCopyScript}
""{hostessFilePath}"" {idList} {string.Join(" ", switches)}
:exit
popd
@echo on
";
        }

        private void ExpandAssetZip(Stream zipFileStream, string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var assetsDirectory = Path.Combine(outputDirectory, "assets");
            if (!Directory.Exists(assetsDirectory))
                Directory.CreateDirectory(assetsDirectory);

            using var zipArchive = new ZipArchive(zipFileStream, ZipArchiveMode.Read);
            zipArchive.ExtractToDirectory(assetsDirectory, true);
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

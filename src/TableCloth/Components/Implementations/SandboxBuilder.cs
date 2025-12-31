using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TableCloth.Models.Answers;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class SandboxBuilder(
    IAppMessageBox appMessageBox,
    IArchiveExpander archiveExpander,
    ISharedLocations sharedLocations) : ISandboxBuilder
{
    private readonly string _wdagUtilityAccountPath = @"C:\Users\WDAGUtilityAccount";

    private string GetAssetsPathForSandbox()
        => Path.Combine(_wdagUtilityAccountPath, "Desktop", "Assets");

    private string GetCertificateStagingPathForSandbox()
        => Path.Combine(_wdagUtilityAccountPath, "Desktop", "Assets", "certs");

    private string GetNPKIPathForSandbox(X509CertPair certPair)
    {
        // Note: 샌드박스 안에서 사용할 경로를 조립하는 것이므로 SHGetKnownFolderPath API를 사용하면 안됩니다.
        var candidatePath = Path.Join("AppData", "LocalLow", "NPKI", certPair.Organization);

        if (certPair.IsPersonalCert)
            candidatePath = Path.Join(candidatePath, "USER", certPair.SubjectNameForNpkiApp);

        return Path.Join(_wdagUtilityAccountPath, candidatePath);
    }

    public async Task<string?> GenerateSandboxConfigurationAsync(
        string outputDirectory,
        TableClothConfiguration tableClothConfiguration,
        IList<SandboxMappedFolder> excludedDirectories,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        // Spork 실행 파일은 assets 디렉터리에 압축 해제
        var sporkZipFilePath = Path.Combine(sharedLocations.ExecutableDirectoryPath, "Spork.zip");
        if (!await ExpandSporkAssetZipAsync(sporkZipFilePath, outputDirectory, cancellationToken).ConfigureAwait(false))
            return default;

        var assetsDirectory = Path.Combine(outputDirectory, "assets");
        if (!Directory.Exists(assetsDirectory))
            Directory.CreateDirectory(assetsDirectory);

        tableClothConfiguration.AssetsDirectoryPath = assetsDirectory;

        var batchFileContent = GenerateSandboxStartupScript(tableClothConfiguration);
        var batchFilePath = Path.Combine(assetsDirectory, "StartupScript.cmd");
        await File.WriteAllTextAsync(batchFilePath, batchFileContent, Encoding.Default, cancellationToken).ConfigureAwait(false);

        var sporkAnswerJsonPath = Path.Combine(assetsDirectory, "SporkAnswers.json");
        var sporkAnswerJsonContent = await SerializeSporkAnswersJsonAsync(new SporkAnswers
        {
            HostUILocale = CultureInfo.CurrentUICulture.Name,

        }, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(sporkAnswerJsonPath, sporkAnswerJsonContent, cancellationToken).ConfigureAwait(false);

        var wsbFilePath = Path.Combine(outputDirectory, "InternetBankingSandbox.wsb");
        var serializedXml = SerializeSandboxSpec(
            await BootstrapSandboxConfigurationAsync(tableClothConfiguration, cancellationToken).ConfigureAwait(false),
            excludedDirectories);
        await File.WriteAllTextAsync(wsbFilePath, serializedXml, cancellationToken).ConfigureAwait(false);

        return wsbFilePath;
    }

    private async Task<SandboxConfiguration> BootstrapSandboxConfigurationAsync(
        TableClothConfiguration tableClothConfig,
        CancellationToken cancellationToken = default)
    {
        const string Enable = "Enable";
        const string Disable = "Disable";

        var sandboxConfig = new SandboxConfiguration
        {
            AudioInput = tableClothConfig.EnableMicrophone ? Enable : Disable,
            VideoInput = tableClothConfig.EnableWebCam ? Enable : Disable,
            PrinterRedirection = tableClothConfig.EnablePrinters ? Enable : Disable,
            VirtualGpu = Disable,
        };

        if (!Directory.Exists(tableClothConfig.AssetsDirectoryPath))
            return sandboxConfig;

        sandboxConfig.MappedFolders.Clear();

        sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
        {
            HostFolder = tableClothConfig.AssetsDirectoryPath,
            ReadOnly = bool.FalseString,
        });

        // 사용자 지정 매핑 폴더 추가
        if (tableClothConfig.MappedFolders != null)
        {
            foreach (var mappedFolder in tableClothConfig.MappedFolders)
            {
                if (!string.IsNullOrWhiteSpace(mappedFolder.HostFolder))
                {
                    sandboxConfig.MappedFolders.Add(new SandboxMappedFolder
                    {
                        HostFolder = mappedFolder.HostFolder,
                        SandboxFolder = mappedFolder.SandboxFolder,
                        ReadOnly = mappedFolder.ReadOnly ? bool.TrueString : bool.FalseString,
                    });
                }
            }
        }

        if (tableClothConfig.CertPair != null &&
            tableClothConfig.CertPair.PublicKey != null &&
            tableClothConfig.CertPair.PrivateKey != null)
        {
            var certStagingDirectoryPath = sharedLocations.GetCertificateStagingDirectoryPath();
            if (Directory.Exists(certStagingDirectoryPath))
                Directory.Delete(certStagingDirectoryPath, true);
            Directory.CreateDirectory(certStagingDirectoryPath);

            var destDerFilePath = Path.Combine(certStagingDirectoryPath, "signCert.der");
            var destKeyFileName = Path.Combine(certStagingDirectoryPath, "signPri.key");

            await File.WriteAllBytesAsync(destDerFilePath, tableClothConfig.CertPair.PublicKey, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(destKeyFileName, tableClothConfig.CertPair.PrivateKey, cancellationToken).ConfigureAwait(false);
        }

        sandboxConfig.LogonCommand.Add(Path.Combine(GetAssetsPathForSandbox(), "StartupScript.cmd"));
        return sandboxConfig;
    }

    private string GenerateSandboxStartupScript(TableClothConfiguration tableClothConfiguration)
    {
        ArgumentNullException.ThrowIfNull(tableClothConfiguration);

        var certFileMoveScript = string.Empty;

        if (tableClothConfiguration.CertPair != null)
        {
            var npkiDirectoryPathInSandbox = GetNPKIPathForSandbox(tableClothConfiguration.CertPair);
            var desktopDirectoryPathInSandbox = "%userprofile%\\Desktop\\Certificates";
            var certStagingPath = GetCertificateStagingPathForSandbox();
            var providedCertFilePath = Path.Combine(certStagingPath, "*.*");
            certFileMoveScript = $@"
if not exist ""{npkiDirectoryPathInSandbox}"" mkdir ""{npkiDirectoryPathInSandbox}""
if not exist ""{desktopDirectoryPathInSandbox}"" mkdir ""{desktopDirectoryPathInSandbox}""
copy /y ""{providedCertFilePath}"" ""{npkiDirectoryPathInSandbox}""
move /y ""{providedCertFilePath}"" ""{desktopDirectoryPathInSandbox}""
rmdir /q ""{certStagingPath}""
";
        }

        var switches = new List<string>();

        if (tableClothConfiguration.InstallEveryonesPrinter)
            switches.Add(ConstantStrings.TableCloth_Switch_InstallEveryonesPrinter);

        if (tableClothConfiguration.InstallAdobeReader)
            switches.Add(ConstantStrings.TableCloth_Switch_InstallAdobeReader);

        if (tableClothConfiguration.InstallHancomOfficeViewer)
            switches.Add(ConstantStrings.TableCloth_Switch_InstallHancomOfficeViewer);

        if (tableClothConfiguration.InstallRaiDrive)
            switches.Add(ConstantStrings.TableCloth_Switch_InstallRaiDrive);

        var serviceIdList = (tableClothConfiguration.Services ?? Enumerable.Empty<CatalogInternetService>())
            .Select(x => x.Id).Distinct();
        var sporkFilePath = Path.Combine(GetAssetsPathForSandbox(), "Spork.exe");
        var idList = string.Join(" ", serviceIdList);

        return $@"@echo off
pushd ""%~dp0""
{certFileMoveScript}
""{sporkFilePath}"" {idList} {string.Join(" ", switches)}
:exit
popd
@echo on
";
    }

    private async Task<bool> ExpandSporkAssetZipAsync(string zipFilePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipFilePath))
            return false;

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var sporkAssetsDirectory = Path.Combine(outputDirectory, "assets");

        if (!Directory.Exists(sporkAssetsDirectory))
            Directory.CreateDirectory(sporkAssetsDirectory);

        try
        {
            await archiveExpander.ExpandArchiveAsync(
                zipFilePath, sporkAssetsDirectory, cancellationToken)
                .ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            appMessageBox.DisplayError(ex, true);
            return false;
        }
    }

    public static async Task<string> SerializeSporkAnswersJsonAsync(SporkAnswers answers, CancellationToken cancellationToken = default)
    {
        using var memStream = new MemoryStream();
        await JsonSerializer.SerializeAsync(memStream, answers, new JsonSerializerOptions() { WriteIndented = true, }, cancellationToken).ConfigureAwait(false);
        return new UTF8Encoding(false).GetString(memStream.ToArray());
    }

    private static string SerializeSandboxSpec(SandboxConfiguration configuration, IList<SandboxMappedFolder> excludedFolders)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var unavailableDirectories = configuration.MappedFolders
            .Where(x => !Directory.Exists(x.HostFolder))
            .ToList();

        configuration.MappedFolders.RemoveAll(x => unavailableDirectories.Contains(x));

        if (excludedFolders != null)
        {
            foreach (var eachDirectory in unavailableDirectories)
                excludedFolders.Add(eachDirectory);
        }

        var configElement = new XElement("Configuration");

        AddElementIfNotNull(configElement, "Networking", configuration.Networking);
        AddElementIfNotNull(configElement, "AudioInput", configuration.AudioInput);
        AddElementIfNotNull(configElement, "VideoInput", configuration.VideoInput);
        AddElementIfNotNull(configElement, "vGPU", configuration.VirtualGpu);
        AddElementIfNotNull(configElement, "PrinterRedirection", configuration.PrinterRedirection);
        AddElementIfNotNull(configElement, "ClipboardRedirection", configuration.ClipboardRedirection);
        AddElementIfNotNull(configElement, "ProtectedClient", configuration.ProtectedClient);

        if (configuration.MemoryInMB.HasValue)
            configElement.Add(new XElement("MemoryInMB", configuration.MemoryInMB.Value));

        if (configuration.MappedFolders.Count > 0)
        {
            var mappedFoldersElement = new XElement("MappedFolders");
            foreach (var folder in configuration.MappedFolders)
            {
                var folderElement = new XElement("MappedFolder",
                    new XElement("HostFolder", folder.HostFolder));

                AddElementIfNotNull(folderElement, "SandboxFolder", folder.SandboxFolder);
                AddElementIfNotNull(folderElement, "ReadOnly", folder.ReadOnly);

                mappedFoldersElement.Add(folderElement);
            }
            configElement.Add(mappedFoldersElement);
        }

        if (configuration.LogonCommand.Count > 0)
        {
            var logonCommandElement = new XElement("LogonCommand");
            foreach (var command in configuration.LogonCommand)
            {
                logonCommandElement.Add(new XElement("Command", command));
            }
            configElement.Add(logonCommandElement);
        }

        var doc = new XDocument(configElement);
        return doc.ToString(SaveOptions.DisableFormatting);
    }

    private static void AddElementIfNotNull(XElement parent, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            parent.Add(new XElement(name, value));
    }
}

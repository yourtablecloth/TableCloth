using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class SandboxLauncher(
    IAppMessageBox appMessageBox,
    ISharedLocations sharedLocations,
    ISandboxBuilder sandboxBuilder,
    ISandboxCleanupManager sandboxCleanupManager,
    ILogger<SandboxLauncher> logger) : ISandboxLauncher
{
    private readonly ILogger _logger = logger;

    public async Task RunSandboxAsync(TableClothConfiguration config, CancellationToken cancellationToken = default)
    {
        var comSpecPath = Helpers.GetDefaultCommandLineInterpreterPath();

        if (!File.Exists(comSpecPath))
        {
            appMessageBox.DisplayError(ErrorStrings.Error_CommandLineInterpreter_Missing, true);

            if (!Helpers.IsDevelopmentBuild)
                return;
        }

        var wsbExecPath = Helpers.GetDefaultWindowsSandboxPath();

        if (!File.Exists(wsbExecPath))
        {
            appMessageBox.DisplayError(ErrorStrings.Error_Windows_Sandbox_Missing, true);

            if (!Helpers.IsDevelopmentBuild)
                return;
        }

        if (Helpers.IsWindowsSandboxRunning())
        {
            appMessageBox.DisplayError(ErrorStrings.Error_Windows_Sandbox_Already_Running, false);
            return;
        }

        if (config.CertPair != null)
        {
            var now = DateTime.Now;
            var expireWindow = StringResources.Cert_ExpireWindow;

            if (now < config.CertPair.NotBefore)
                appMessageBox.DisplayError(StringResources.Error_Cert_MayTooEarly(now, config.CertPair.NotBefore), false);

            if (now > config.CertPair.NotAfter)
                appMessageBox.DisplayError(ErrorStrings.Error_Cert_Expired, false);
            else if (now > config.CertPair.NotAfter.Add(expireWindow))
                appMessageBox.DisplayInfo(StringResources.Error_Cert_ExpireSoon(now, config.CertPair.NotAfter, expireWindow));
        }

        var tempPath = sharedLocations.GetTempPath();
        var excludedFolderList = new List<SandboxMappedFolder>();

        var wsbFilePath = await sandboxBuilder.GenerateSandboxConfigurationAsync(
            tempPath, config, excludedFolderList, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(wsbFilePath))
        {
            appMessageBox.DisplayError(ErrorStrings.Error_Fail_PrepareAssetDirectory, true);
            return;
        }

        if (excludedFolderList.Count != 0)
            appMessageBox.DisplayError(StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

        sandboxCleanupManager.SetWorkingDirectory(tempPath);

        if (!ValidateSandboxSpecFile(wsbFilePath, out string? reason))
        {
            appMessageBox.DisplayError(reason, true);
            return;
        }

        var process = Helpers.CreateRunProcess(comSpecPath, wsbExecPath, wsbFilePath);

        if (!process.Start())
        {
            process.Dispose();
            appMessageBox.DisplayError(ErrorStrings.Error_Windows_Sandbox_CanNotStart, true);
        }
    }

    public bool ValidateSandboxSpecFile(string wsbFilePath, out string? reason)
    {
        try
        {
            if (!File.Exists(wsbFilePath))
            {
                reason = StringResources.TableCloth_Log_WsbFileCreateFail_ProhibitTranslation(wsbFilePath);
                _logger.LogError("{reason}", reason);
                return false;
            }

            var doc = XDocument.Load(wsbFilePath);
            var root = doc.Root;

            if (root == null || root.Name.LocalName != "Configuration")
            {
                reason = StringResources.TableCloth_Log_CannotParseWsbFile_ProhibitTranslation(wsbFilePath);
                _logger.LogError("{reason}", reason);
                return false;
            }

            var mappedFoldersElement = root.Element("MappedFolders");
            if (mappedFoldersElement != null)
            {
                foreach (var mappedFolderElement in mappedFoldersElement.Elements("MappedFolder"))
                {
                    var hostFolder = mappedFolderElement.Element("HostFolder")?.Value;

                    // HostFolder 태그에 들어가는 경로는 절대 경로만 사용되므로 상대 경로 처리를 하지 않아도 됨.
                    if (!string.IsNullOrEmpty(hostFolder) && !Directory.Exists(hostFolder))
                    {
                        reason = StringResources.TableCloth_Log_HostFolderNotExists_ProhibitTranslation(hostFolder);
                        _logger.LogError("{reason}", reason);
                        return false;
                    }
                }
            }

            reason = null;
            return true;
        }
        catch (Exception ex)
        {
            var actualException = ex;

            if (ex is AggregateException)
                actualException = ex.InnerException ?? ex;

            reason = StringResources.TableCloth_UnwrapException(actualException);
            _logger.LogError(actualException, "{reason}", reason);
            return false;
        }
    }
}

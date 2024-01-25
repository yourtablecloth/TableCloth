using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;

namespace TableCloth.Components;

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

        if (Helpers.GetSandboxRunningState())
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

            var content = default(SandboxConfiguration);

            using (var fileStream = File.OpenRead(wsbFilePath))
            {
                var serializer = new XmlSerializer(typeof(SandboxConfiguration));
                content = serializer.Deserialize(fileStream) as SandboxConfiguration;

                if (content == null)
                {
                    reason = StringResources.TableCloth_Log_CannotParseWsbFile_ProhibitTranslation(wsbFilePath);
                    _logger.LogError("{reason}", reason);
                    return false;
                }
            }

            foreach (var eachMappedFolder in content.MappedFolders)
            {
                // HostFolder 태그에 들어가는 경로는 절대 경로만 사용되므로 상대 경로 처리를 하지 않아도 됨.
                if (!Directory.Exists(eachMappedFolder.HostFolder))
                {
                    reason = StringResources.TableCloth_Log_HostFolderNotExists_ProhibitTranslation(eachMappedFolder.HostFolder);
                    _logger.LogError("{reason}", reason);
                    return false;
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

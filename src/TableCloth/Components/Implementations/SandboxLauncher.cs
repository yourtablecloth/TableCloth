using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    public void RunSandbox(TableClothConfiguration config)
    {
        if (Helpers.GetSandboxRunningState())
        {
            appMessageBox.DisplayError(StringResources.Error_Windows_Sandbox_Already_Running, false);
            return;
        }

        if (config.CertPair != null)
        {
            var now = DateTime.Now;
            var expireWindow = StringResources.Cert_ExpireWindow;

            if (now < config.CertPair.NotBefore)
                appMessageBox.DisplayError(StringResources.Error_Cert_MayTooEarly(now, config.CertPair.NotBefore), false);

            if (now > config.CertPair.NotAfter)
                appMessageBox.DisplayError(StringResources.Error_Cert_Expired, false);
            else if (now > config.CertPair.NotAfter.Add(expireWindow))
                appMessageBox.DisplayInfo(StringResources.Error_Cert_ExpireSoon(now, config.CertPair.NotAfter, expireWindow));
        }

        var tempPath = sharedLocations.GetTempPath();
        var excludedFolderList = new List<SandboxMappedFolder>();
        var wsbFilePath = sandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

        if (excludedFolderList.Any())
            appMessageBox.DisplayError(StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

        sandboxCleanupManager.SetWorkingDirectory(tempPath);

        if (!ValidateSandboxSpecFile(wsbFilePath, out string? reason))
        {
            appMessageBox.DisplayError(reason, true);
            return;
        }

        var comSpecPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "cmd.exe");

        var process = new Process()
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo(comSpecPath, "/c start \"\" \"" + wsbFilePath + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        if (!process.Start())
        {
            process.Dispose();
            appMessageBox.DisplayError(StringResources.Error_Windows_Sandbox_CanNotStart, true);
        }
    }

    public bool ValidateSandboxSpecFile(string wsbFilePath, out string? reason)
    {
        try
        {
            if (!File.Exists(wsbFilePath))
            {
                _logger.LogError(reason = StringResources.TableCloth_Log_WsbFileCreateFail_ProhibitTranslation(wsbFilePath));
                return false;
            }

            var content = default(SandboxConfiguration);

            using (var fileStream = File.OpenRead(wsbFilePath))
            {
                var serializer = new XmlSerializer(typeof(SandboxConfiguration));
                content = serializer.Deserialize(fileStream) as SandboxConfiguration;

                if (content == null)
                {
                    _logger.LogError(reason = StringResources.TableCloth_Log_CannotParseWsbFile_ProhibitTranslation(wsbFilePath));
                    return false;
                }
            }

            foreach (var eachMappedFolder in content.MappedFolders)
            {
                // HostFolder 태그에 들어가는 경로는 절대 경로만 사용되므로 상대 경로 처리를 하지 않아도 됨.
                if (!Directory.Exists(eachMappedFolder.HostFolder))
                {
                    _logger.LogError(reason = StringResources.TableCloth_Log_HostFolderNotExists_ProhibitTranslation(eachMappedFolder.HostFolder));
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

            _logger.LogError(actualException, reason = StringResources.TableCloth_UnwrapException(actualException));
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.Components;
using TableCloth.Contracts;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;

namespace TableCloth.Commands;

public sealed class LaunchSandboxCommand : CommandBase
{
    public LaunchSandboxCommand(
        SandboxLauncher sandboxLauncher,
        AppMessageBox appMessageBox,
        SharedLocations sharedLocations,
        SandboxBuilder sandboxBuilder,
        SandboxCleanupManager sandboxCleanupManager)
    {
        _sandboxLauncher = sandboxLauncher;
        _appMessageBox = appMessageBox;
        _sharedLocations = sharedLocations;
        _sandboxBuilder = sandboxBuilder;
        _sandboxCleanupManager = sandboxCleanupManager;
    }

    private readonly SandboxLauncher _sandboxLauncher;
    private readonly AppMessageBox _appMessageBox;
    private readonly SharedLocations _sharedLocations;
    private readonly SandboxBuilder _sandboxBuilder;
    private readonly SandboxCleanupManager _sandboxCleanupManager;

    protected override bool EvaluateCanExecute()
        => (!_sandboxLauncher.IsSandboxRunning());

    public override void Execute(object? parameter)
    {
        if (_sandboxLauncher.IsSandboxRunning())
        {
            _appMessageBox.DisplayError(StringResources.Error_Windows_Sandbox_Already_Running, false);
            return;
        }

        var viewModel = parameter as ICanComposeConfiguration;

        if (viewModel == null)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        var config = viewModel.GetTableClothConfiguration();

        if (config.CertPair != null)
        {
            var now = DateTime.Now;
            var expireWindow = StringResources.Cert_ExpireWindow;

            if (now < config.CertPair.NotBefore)
                _appMessageBox.DisplayError(StringResources.Error_Cert_MayTooEarly(now, config.CertPair.NotBefore), false);

            if (now > config.CertPair.NotAfter)
                _appMessageBox.DisplayError(StringResources.Error_Cert_Expired, false);
            else if (now > config.CertPair.NotAfter.Add(expireWindow))
                _appMessageBox.DisplayInfo(StringResources.Error_Cert_ExpireSoon(now, config.CertPair.NotAfter, expireWindow));
        }

        var tempPath = _sharedLocations.GetTempPath();
        var excludedFolderList = new List<SandboxMappedFolder>();
        var wsbFilePath = _sandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

        if (excludedFolderList.Any())
            _appMessageBox.DisplayError(StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

        _sandboxCleanupManager.SetWorkingDirectory(tempPath);
        _sandboxLauncher.RunSandbox(wsbFilePath);
    }
}

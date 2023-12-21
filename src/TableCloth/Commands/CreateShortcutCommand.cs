using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands;

public sealed class CreateShortcutCommand : CommandBase
{
    public CreateShortcutCommand(
        CommandLineComposer commandLineComposer,
        SharedLocations sharedLocations,
        AppMessageBox appMessageBox)
    {
        _commandLineComposer = commandLineComposer;
        _sharedLocations = sharedLocations;
        _appMessageBox = appMessageBox;
    }

    private readonly CommandLineComposer _commandLineComposer;
    private readonly SharedLocations _sharedLocations;
    private readonly AppMessageBox _appMessageBox;

    public override void Execute(object? parameter)
    {
        switch (parameter)
        {
            case MainWindowViewModel v1ViewModel:
                this.ExecuteFromV1Model(v1ViewModel);
                break;

            case DetailPageViewModel v2ViewModel:
                this.ExecuteFromV2Model(v2ViewModel);
                break;

            default:
                throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));
        }
    }

    private void ExecuteFromV1Model(MainWindowViewModel viewModel)
    {
        var options = new List<string?>();
        var mainModule = Process.GetCurrentProcess().MainModule
            ?? throw new Exception("Cannot obtain current process main module information.");
        var targetPath = mainModule.FileName;
        var linkName = StringResources.AppName;

        if (viewModel.EnableMicrophone)
            options.Add(StringResources.TableCloth_Switch_EnableMicrophone);
        if (viewModel.EnableWebCam)
            options.Add(StringResources.TableCloth_Switch_EnableCamera);
        if (viewModel.EnablePrinters)
            options.Add(StringResources.TableCloth_Switch_EnablePrinter);
        if (viewModel.InstallEveryonesPrinter)
            options.Add(StringResources.TableCloth_Switch_InstallEveryonesPrinter);
        if (viewModel.InstallAdobeReader)
            options.Add(StringResources.TableCloth_Switch_InstallAdobeReader);
        if (viewModel.InstallHancomOfficeViewer)
            options.Add(StringResources.TableCloth_Switch_InstallHancomOfficeViewer);
        if (viewModel.InstallRaiDrive)
            options.Add(StringResources.TableCloth_Switch_InstallRaiDrive);
        if (viewModel.EnableInternetExplorerMode)
            options.Add(StringResources.TableCloth_Switch_EnableIEMode);
        if (viewModel.MapNpkiCert)
            options.Add(StringResources.Tablecloth_Switch_EnableCert);

        // 단축 아이콘은 지정 가능한 명령줄의 길이가 260자가 최대인 관계로 여러 사이트를 지정하는 것이 어려움.
        var firstSite = viewModel.SelectedServices?.FirstOrDefault();
        var iconFilePath = default(string);

        if (firstSite != null)
        {
            options.Add(firstSite.Id);
            linkName = firstSite.DisplayName;

            iconFilePath = Path.Combine(
                _sharedLocations.GetImageDirectoryPath(),
                $"{firstSite.Id}.ico");

            if (!File.Exists(iconFilePath))
                iconFilePath = null;
        }

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fullPath = Path.Combine(desktopPath, linkName + ".lnk");

        for (int i = 1; File.Exists(fullPath); ++i)
            fullPath = Path.Combine(desktopPath, linkName + $" ({i}).lnk");

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new Exception("Cannot obtain WScript.Shell object type information.");
            var shell = Activator.CreateInstance(shellType)
                ?? throw new Exception("Cannot obtain WScript.Shell object instance.");
            dynamic shortcut = ((dynamic)shell).CreateShortcut(fullPath);
            shortcut.TargetPath = targetPath;

            if (iconFilePath != null && File.Exists(iconFilePath))
                shortcut.IconLocation = iconFilePath;

            shortcut.Arguments = String.Join(' ', options.ToArray());
            shortcut.Save();
        }
        catch
        {
            _appMessageBox.DisplayInfo(StringResources.Error_ShortcutFailed);
            return;
        }

        _appMessageBox.DisplayInfo(StringResources.Info_ShortcutSuccess);
    }

    private void ExecuteFromV2Model(DetailPageViewModel viewModel)
    {
        var targetPath = _sharedLocations.ExecutableFilePath;
        var linkName = StringResources.AppName;

        var firstSite = viewModel.SelectedService;
        var iconFilePath = default(string);

        if (firstSite != null)
        {
            linkName = firstSite.DisplayName;

            iconFilePath = Path.Combine(
                _sharedLocations.GetImageDirectoryPath(),
                $"{firstSite.Id}.ico");

            if (!File.Exists(iconFilePath))
                iconFilePath = null;
        }

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fullPath = Path.Combine(desktopPath, linkName + ".lnk");

        for (int i = 1; File.Exists(fullPath); ++i)
            fullPath = Path.Combine(desktopPath, linkName + $" ({i}).lnk");

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new Exception("Cannot obtain WScript.Shell object type information.");
            var shell = Activator.CreateInstance(shellType)
                ?? throw new Exception("Cannot obtain WScript.Shell object instance.");
            dynamic shortcut = ((dynamic)shell).CreateShortcut(fullPath);
            shortcut.TargetPath = targetPath;

            if (iconFilePath != null && File.Exists(iconFilePath))
                shortcut.IconLocation = iconFilePath;

            shortcut.Arguments = _commandLineComposer.ComposeCommandLineArguments(viewModel);
            shortcut.Save();
        }
        catch
        {
            _appMessageBox.DisplayInfo(StringResources.Error_ShortcutFailed);
            return;
        }

        _appMessageBox.DisplayInfo(StringResources.Info_ShortcutSuccess);
    }
}

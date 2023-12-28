using System.Collections.Generic;
using System.Linq;

namespace TableCloth.Models;

public sealed class CommandLineArgumentModel
{
    public CommandLineArgumentModel(
        string[]? selectedServices = default,
        bool? enableMicrophone = default,
        bool? enableWebCam = default,
        bool? enablePrinters = default,
        string? certPrivateKeyPath = default,
        string? certPublicKeyPath = default,
        bool? installEveryonesPrinter = default,
        bool? installAdobeReader = default,
        bool? installHancomOfficeViewer = default,
        bool? installRaiDrive = default,
        bool? enableInternetExplorerMode = default,
        bool showCommandLineHelp = default)
    {
        SelectedServices = selectedServices ?? Enumerable.Empty<string>();
        EnableMicrophone = enableMicrophone;
        EnableWebCam = enableWebCam;
        EnablePrinters = enablePrinters;
        CertPrivateKeyPath = certPrivateKeyPath;
        CertPublicKeyPath = certPublicKeyPath;
        InstallEveryonesPrinter = installEveryonesPrinter;
        InstallAdobeReader = installAdobeReader;
        InstallHancomOfficeViewer = installHancomOfficeViewer;
        InstallRaiDrive = installRaiDrive;
        EnableInternetExplorerMode = enableInternetExplorerMode;
        ShowCommandLineHelp = showCommandLineHelp;
    }

    public bool? EnableMicrophone { get; private set; }

    public bool? EnableWebCam { get; private set; }

    public bool? EnablePrinters { get; private set; }

    public string? CertPrivateKeyPath { get; private set; }

    public string? CertPublicKeyPath { get; private set; }

    public bool? InstallEveryonesPrinter { get; private set; }

    public bool? InstallAdobeReader { get; private set; }

    public bool? InstallHancomOfficeViewer { get; private set; }

    public bool? InstallRaiDrive { get; private set; }

    public bool? EnableInternetExplorerMode { get; private set; }

    public bool ShowCommandLineHelp { get; private set; }

    public IEnumerable<string> SelectedServices { get; private set; } = new List<string>();
}

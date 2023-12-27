using System.Collections.Generic;

namespace TableCloth.Contracts;

public interface ITableClothArgumentModel
{
    bool BuiltFromCommandLine { get; }
    bool ShowCommandLineHelp { get; }
    bool? EnableMicrophone { get; }
    bool? EnableWebCam { get; }
    bool? EnablePrinters { get; }
    bool? InstallEveryonesPrinter { get; }
    bool? InstallAdobeReader { get; }
    bool? InstallHancomOfficeViewer { get; }
    bool? InstallRaiDrive { get; }
    bool? EnableInternetExplorerMode { get; }
    string? CertPublicKeyPath { get; }
    string? CertPrivateKeyPath { get; }
    IEnumerable<string> SelectedServices { get; }
}

using System.Collections.Generic;
using TableCloth.Models.Catalog;

namespace TableCloth.Contracts;

public interface ICommandLineArgumentModel
{
    bool? EnableMicrophone { get; }
    bool? EnableWebCam { get; }
    bool? EnablePrinters { get; }
    bool? InstallEveryonesPrinter { get; }
    bool? InstallAdobeReader { get; }
    bool? InstallHancomOfficeViewer { get; }
    bool? InstallRaiDrive { get; }
    bool? EnableInternetExplorerMode { get; }
    bool? MapNpkiCert { get; }
    IEnumerable<string> SelectedServices { get; }
}

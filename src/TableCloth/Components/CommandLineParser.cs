using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableCloth.Models;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class CommandLineParser
{
    public CommandLineParser(
        CatalogCacheManager catalogCacheManager)
    {
        this._catalogCacheManager = catalogCacheManager;
    }

    private readonly CatalogCacheManager _catalogCacheManager;

    public MainWindowArgumentModel ParseForV1(string[] args)
    {
        var services = _catalogCacheManager.CatalogDocument?.Services;

        var selectedServiceIds = new List<string>();
        var enableMicrophone = default(bool?);
        var enableWebCam = default(bool?);
        var enablePrinters = default(bool?);
        var certPrivateKeyPath = default(string);
        var certPublicKeyPath = default(string);
        var installEveryonesPrinter = default(bool?);
        var installAdobeReader = default(bool?);
        var installHancomOfficeViewer = default(bool?);
        var installRaiDrive = default(bool?);
        var enableInternetExplorerMode = default(bool?);
        var showCommandLineHelp = false;
        var enableCert = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith(StringResources.TableCloth_Switch_Prefix))
                selectedServiceIds.Add(args[i]);
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableMicrophone, StringComparison.OrdinalIgnoreCase))
                enableMicrophone = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableCamera, StringComparison.OrdinalIgnoreCase))
                enableWebCam = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnablePrinter, StringComparison.OrdinalIgnoreCase))
                enablePrinters = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_CertPrivateKey, StringComparison.OrdinalIgnoreCase))
                certPrivateKeyPath = args[Math.Min(args.Length - 1, ++i)];
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_CertPublicKey, StringComparison.OrdinalIgnoreCase))
                certPublicKeyPath = args[Math.Min(args.Length - 1, ++i)];
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallEveryonesPrinter, StringComparison.OrdinalIgnoreCase))
                installEveryonesPrinter = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallAdobeReader, StringComparison.OrdinalIgnoreCase))
                installAdobeReader = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallHancomOfficeViewer, StringComparison.OrdinalIgnoreCase))
                installHancomOfficeViewer = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallRaiDrive, StringComparison.OrdinalIgnoreCase))
                installRaiDrive = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableIEMode, StringComparison.OrdinalIgnoreCase))
                enableInternetExplorerMode = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_Help, StringComparison.OrdinalIgnoreCase))
                showCommandLineHelp = true;
            else if (string.Equals(args[i], StringResources.Tablecloth_Switch_EnableCert, StringComparison.OrdinalIgnoreCase))
                enableCert = true;
        }

        var selectedServices = services?.Where(x => selectedServiceIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        var certPair = default(X509CertPair);

        if (enableCert)
        {
            var certPublicKeyData = default(byte[]);
            var certPrivateKeyData = default(byte[]);

            if (File.Exists(certPublicKeyPath))
                certPublicKeyData = File.ReadAllBytes(certPublicKeyPath);

            if (File.Exists(certPrivateKeyPath))
                certPrivateKeyData = File.ReadAllBytes(certPrivateKeyPath);

            if (certPublicKeyData != null && certPrivateKeyData != null)
                certPair = new X509CertPair(certPublicKeyData, certPrivateKeyData);
            else
                certPair = null;
        }

        return new MainWindowArgumentModel(
            selectedServices,
            enableMicrophone: enableMicrophone,
            enableWebCam: enableWebCam,
            enablePrinters: enablePrinters,
            certPrivateKeyPath: certPrivateKeyPath,
            certPublicKeyPath: certPublicKeyPath,
            installEveryonesPrinter: installEveryonesPrinter,
            installAdobeReader: installAdobeReader,
            installHancomOfficeViewer: installHancomOfficeViewer,
            installRaiDrive: installRaiDrive,
            enableInternetExplorerMode: enableInternetExplorerMode,
            showCommandLineHelp: showCommandLineHelp,
            builtFromCommandLine: true);
    }

    public DetailPageArgumentModel Parse(string[] args)
    {
        var services = _catalogCacheManager.CatalogDocument?.Services;

        var selectedServiceIds = new List<string>();
        var enableMicrophone = default(bool?);
        var enableWebCam = default(bool?);
        var enablePrinters = default(bool?);
        var certPrivateKeyPath = default(string);
        var certPublicKeyPath = default(string);
        var installEveryonesPrinter = default(bool?);
        var installAdobeReader = default(bool?);
        var installHancomOfficeViewer = default(bool?);
        var installRaiDrive = default(bool?);
        var enableInternetExplorerMode = default(bool?);
        var showCommandLineHelp = false;
        var enableCert = false;

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith(StringResources.TableCloth_Switch_Prefix))
                selectedServiceIds.Add(args[i]);
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableMicrophone, StringComparison.OrdinalIgnoreCase))
                enableMicrophone = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableCamera, StringComparison.OrdinalIgnoreCase))
                enableWebCam = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnablePrinter, StringComparison.OrdinalIgnoreCase))
                enablePrinters = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_CertPrivateKey, StringComparison.OrdinalIgnoreCase))
                certPrivateKeyPath = args[Math.Min(args.Length - 1, ++i)];
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_CertPublicKey, StringComparison.OrdinalIgnoreCase))
                certPublicKeyPath = args[Math.Min(args.Length - 1, ++i)];
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallEveryonesPrinter, StringComparison.OrdinalIgnoreCase))
                installEveryonesPrinter = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallAdobeReader, StringComparison.OrdinalIgnoreCase))
                installAdobeReader = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallHancomOfficeViewer, StringComparison.OrdinalIgnoreCase))
                installHancomOfficeViewer = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallRaiDrive, StringComparison.OrdinalIgnoreCase))
                installRaiDrive = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableIEMode, StringComparison.OrdinalIgnoreCase))
                enableInternetExplorerMode = true;
            else if (string.Equals(args[i], StringResources.TableCloth_Switch_Help, StringComparison.OrdinalIgnoreCase))
                showCommandLineHelp = true;
            else if (string.Equals(args[i], StringResources.Tablecloth_Switch_EnableCert, StringComparison.OrdinalIgnoreCase))
                enableCert = true;
        }

        var certPair = default(X509CertPair);

        if (enableCert)
        {
            var certPublicKeyData = default(byte[]);
            var certPrivateKeyData = default(byte[]);

            if (File.Exists(certPublicKeyPath))
                certPublicKeyData = File.ReadAllBytes(certPublicKeyPath);

            if (File.Exists(certPrivateKeyPath))
                certPrivateKeyData = File.ReadAllBytes(certPrivateKeyPath);

            if (certPublicKeyData != null && certPrivateKeyData != null)
                certPair = new X509CertPair(certPublicKeyData, certPrivateKeyData);
            else
                certPair = null;
        }

        var selectedService = services?.Where(x => selectedServiceIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList().FirstOrDefault();

        return new DetailPageArgumentModel(
            selectedService,
            enableMicrophone: enableMicrophone,
            enableWebCam: enableWebCam,
            enablePrinters: enablePrinters,
            certPrivateKeyPath: certPrivateKeyPath,
            certPublicKeyPath: certPublicKeyPath,
            installEveryonesPrinter: installEveryonesPrinter,
            installAdobeReader: installAdobeReader,
            installHancomOfficeViewer: installHancomOfficeViewer,
            installRaiDrive: installRaiDrive,
            enableInternetExplorerMode: enableInternetExplorerMode,
            showCommandLineHelp: showCommandLineHelp,
            builtFromCommandLine: true);
    }
}

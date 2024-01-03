using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Models
{
    public sealed class CommandLineArgumentModel
    {
        public CommandLineArgumentModel(
            string[]
#if !NETFX
            ?
#endif
            selectedServices = default,
            bool? enableMicrophone = default,
            bool? enableWebCam = default,
            bool? enablePrinters = default,

            string
#if !NETFX
            ?
#endif
            certPrivateKeyPath = default,

            string
#if !NETFX
            ?
#endif
            certPublicKeyPath = default,

            bool? installEveryonesPrinter = default,
            bool? installAdobeReader = default,
            bool? installHancomOfficeViewer = default,
            bool? installRaiDrive = default,
            bool? enableInternetExplorerMode = default,
            bool showCommandLineHelp = default,
            bool dryRun = default)
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
            DryRun = dryRun;
        }

        public bool? EnableMicrophone { get; private set; }

        public bool? EnableWebCam { get; private set; }

        public bool? EnablePrinters { get; private set; }

        public string
#if !NETFX
            ?
#endif
            CertPrivateKeyPath
        { get; private set; }

        public string
#if !NETFX
            ?
#endif
            CertPublicKeyPath
        { get; private set; }

        public bool? InstallEveryonesPrinter { get; private set; }

        public bool? InstallAdobeReader { get; private set; }

        public bool? InstallHancomOfficeViewer { get; private set; }

        public bool? InstallRaiDrive { get; private set; }

        public bool? EnableInternetExplorerMode { get; private set; }

        public bool ShowCommandLineHelp { get; private set; }

        public IEnumerable<string> SelectedServices { get; private set; } = new List<string>();

        public bool DryRun { get; private set; }

        public static CommandLineArgumentModel ParseFromArgv()
            => Parse(Helpers.GetCommandLineArguments());

        public static CommandLineArgumentModel Parse(string[] args)
        {
            if (args.Length < 1)
                return new CommandLineArgumentModel();

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
            var dryRun = false;

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
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_DryRun, StringComparison.OrdinalIgnoreCase))
                    dryRun = true;
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

            return new CommandLineArgumentModel(
                selectedServices: selectedServiceIds?.ToArray() ?? new string[] { },
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
                dryRun: dryRun);
        }
    }
}

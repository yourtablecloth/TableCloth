using System;
using System.CommandLine.Builder;
using System.CommandLine;
using TableCloth;
using TableCloth.Models;
using System.Linq;
using System.CommandLine.Parsing;

namespace Spork.Components.Implementations
{
    public sealed class CommandLineArguments : ICommandLineArguments
    {
        public CommandLineArgumentModel GetCurrent()
        {
            var args = Helpers.GetCommandLineArguments();

            var enableMicrophoneOption = new Option<bool?>("--enable-microphone")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var enableCameraOption = new Option<bool?>("--enable-camera")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var enablePrinterOption = new Option<bool?>("--enable-printer")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var certPrivateKeyOption = new Option<string>("--cert-private-key")
            { IsRequired = false, Arity = ArgumentArity.ExactlyOne, };

            var certPublicKeyOption = new Option<string>("--cert-public-key")
            { IsRequired = false, Arity = ArgumentArity.ExactlyOne, };

            var installEveryonesPrinterOption = new Option<bool?>("--install-everyones-printer")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var installAdobeReaderOption = new Option<bool?>("--install-adobe-reader")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var installHancomOfficeViewerOption = new Option<bool?>("--install-hancom-office-viewer")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var installRaiDriveOption = new Option<bool?>("--install-rai-drive")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var enableIEModeOption = new Option<bool?>("--enable-ie-mode")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var dryRunOption = new Option<bool>("--dry-run")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var simulateFailureOption = new Option<bool>("--simulate-failure")
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            var siteIdListArgument = new Argument<string[]>()
            { Arity = ArgumentArity.ZeroOrMore, };

            var rootCommand = new RootCommand();
            rootCommand.AddOption(enableMicrophoneOption);
            rootCommand.AddOption(enableCameraOption);
            rootCommand.AddOption(enablePrinterOption);
            rootCommand.AddOption(certPrivateKeyOption);
            rootCommand.AddOption(certPublicKeyOption);
            rootCommand.AddOption(installEveryonesPrinterOption);
            rootCommand.AddOption(installAdobeReaderOption);
            rootCommand.AddOption(installHancomOfficeViewerOption);
            rootCommand.AddOption(installRaiDriveOption);
            rootCommand.AddOption(enableIEModeOption);
            rootCommand.AddOption(dryRunOption);
            rootCommand.AddOption(simulateFailureOption);
            rootCommand.AddArgument(siteIdListArgument);

            var commandLineBuilder = new CommandLineBuilder(rootCommand).UseDefaults().UseHelp().UseVersionOption().UseLocalizationResources(LocalizationResources.Instance);
            var helpOption = rootCommand.Options.FirstOrDefault(x => x.Aliases.Contains("--help", StringComparer.OrdinalIgnoreCase));
            var versionOption = rootCommand.Options.FirstOrDefault(x => x.Aliases.Contains("--version", StringComparer.OrdinalIgnoreCase));

            var parser = commandLineBuilder.Build();
            var parseResult = parser.Parse(args);

            var helpOptionFound = helpOption != null && parseResult.HasOption(helpOption);
            var versionOptionFound = versionOption != null && parseResult.HasOption(versionOption);

            return new CommandLineArgumentModel(args,
                selectedServices: parseResult.GetValueForArgument(siteIdListArgument),
                enableMicrophone: parseResult.GetValueForOption(enableMicrophoneOption),
                enableWebCam: parseResult.GetValueForOption(enableCameraOption),
                enablePrinters: parseResult.GetValueForOption(enablePrinterOption),
                certPrivateKeyPath: parseResult.GetValueForOption(certPrivateKeyOption),
                certPublicKeyPath: parseResult.GetValueForOption(certPublicKeyOption),
                installEveryonesPrinter: parseResult.GetValueForOption(installEveryonesPrinterOption),
                installAdobeReader: parseResult.GetValueForOption(installAdobeReaderOption),
                installHancomOfficeViewer: parseResult.GetValueForOption(installHancomOfficeViewerOption),
                installRaiDrive: parseResult.GetValueForOption(installRaiDriveOption),
                enableInternetExplorerMode: parseResult.GetValueForOption(enableIEModeOption),
                showCommandLineHelp: helpOptionFound,
                dryRun: parseResult.GetValueForOption(dryRunOption),
                simulateFailure: parseResult.GetValueForOption(simulateFailureOption));
        }
    }
}

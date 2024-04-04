﻿using System;
using System.CommandLine.Builder;
using System.CommandLine;
using TableCloth;
using TableCloth.Models;
using System.Linq;
using System.CommandLine.Parsing;
using System.CommandLine.IO;
using TableCloth.Resources;
using System.Threading.Tasks;

namespace Spork.Components.Implementations
{
    public sealed class CommandLineArguments : ICommandLineArguments
    {
        public CommandLineArguments()
        {
            _enableMicrophoneOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_EnableMicrophone)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _enableCameraOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_EnableCamera)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _enablePrinterOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_EnablePrinter)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _certPrivateKeyOption = new Option<string>(ConstantStrings.TableCloth_Switch_CertPrivateKey)
            { IsRequired = false, Arity = ArgumentArity.ExactlyOne, };

            _certPublicKeyOption = new Option<string>(ConstantStrings.TableCloth_Switch_CertPublicKey)
            { IsRequired = false, Arity = ArgumentArity.ExactlyOne, };

            _installEveryonesPrinterOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_InstallEveryonesPrinter)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _installAdobeReaderOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_InstallAdobeReader)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _installHancomOfficeViewerOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_InstallHancomOfficeViewer)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _installRaiDriveOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_InstallRaiDrive)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _enableIEModeOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_EnableIEMode)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _dryRunOption = new Option<bool>(ConstantStrings.TableCloth_Switch_DryRun)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _simulateFailureOption = new Option<bool>(ConstantStrings.TableCloth_Switch_SimulateFailure)
            { IsRequired = false, Arity = ArgumentArity.Zero, };

            _siteIdListArgument = new Argument<string[]>()
            { Arity = ArgumentArity.ZeroOrMore, };

            _rootCommand = new RootCommand()
            {
                _enableMicrophoneOption,
                _enableCameraOption,
                _enablePrinterOption,
                _certPrivateKeyOption,
                _certPublicKeyOption,
                _installEveryonesPrinterOption,
                _installAdobeReaderOption,
                _installHancomOfficeViewerOption,
                _installRaiDriveOption,
                _enableIEModeOption,
                _dryRunOption,
                _simulateFailureOption,
                _siteIdListArgument,
            };

            _commandLineBuilder = new CommandLineBuilder(_rootCommand)
                .UseDefaults()
                .UseHelp()
                .UseVersionOption()
                .UseLocalizationResources(LocalizationResources.Instance);

            _helpOption = _rootCommand.Options
                .FirstOrDefault(x => x.Aliases.Contains(ConstantStrings.TableCloth_Switch_Help, StringComparer.OrdinalIgnoreCase))
                ?? throw new Exception("Unexpected Error: Cannot find help switch from command line parser configuration.");

            _versionOption = _rootCommand.Options
                .FirstOrDefault(x => x.Aliases.Contains(ConstantStrings.TableCloth_Switch_Version, StringComparer.OrdinalIgnoreCase))
                ?? throw new Exception("Unexpected Error: Cannot find version switch from command line parser configuration.");
        }

        private readonly Option<bool?> _enableMicrophoneOption;
        private readonly Option<bool?> _enableCameraOption;
        private readonly Option<bool?> _enablePrinterOption;
        private readonly Option<string> _certPrivateKeyOption;
        private readonly Option<string> _certPublicKeyOption;
        private readonly Option<bool?> _installEveryonesPrinterOption;
        private readonly Option<bool?> _installAdobeReaderOption;
        private readonly Option<bool?> _installHancomOfficeViewerOption;
        private readonly Option<bool?> _installRaiDriveOption;
        private readonly Option<bool?> _enableIEModeOption;
        private readonly Option<bool> _dryRunOption;
        private readonly Option<bool> _simulateFailureOption;
        private readonly Argument<string[]> _siteIdListArgument;
        private readonly RootCommand _rootCommand;
        private readonly CommandLineBuilder _commandLineBuilder;
        private readonly Option _helpOption;
        private readonly Option _versionOption;

        private ParseResult ParseCommandLine(string[] args)
            => _commandLineBuilder.Build().Parse(args);

        public async Task<string> GetHelpStringAsync()
        {
            var testConsole = new TestConsole();
            await ParseCommandLine(new string[] { ConstantStrings.TableCloth_Switch_Help }).InvokeAsync(testConsole).ConfigureAwait(false);
            return testConsole.Out.ToString() ?? string.Empty;
        }

        public async Task<string> GetVersionStringAsync()
        {
            var testConsole = new TestConsole();
            await ParseCommandLine(new string[] { ConstantStrings.TableCloth_Switch_Version }).InvokeAsync(testConsole).ConfigureAwait(false);
            return testConsole.Out.ToString() ?? string.Empty;
        }

        public CommandLineArgumentModel GetCurrent()
        {
            var args = Helpers.GetCommandLineArguments();
            var parseResult = ParseCommandLine(args);

            return new CommandLineArgumentModel(args,
                selectedServices: parseResult.GetValueForArgument(_siteIdListArgument),
                enableMicrophone: parseResult.GetValueForOption(_enableMicrophoneOption),
                enableWebCam: parseResult.GetValueForOption(_enableCameraOption),
                enablePrinters: parseResult.GetValueForOption(_enablePrinterOption),
                certPrivateKeyPath: parseResult.GetValueForOption(_certPrivateKeyOption),
                certPublicKeyPath: parseResult.GetValueForOption(_certPublicKeyOption),
                installEveryonesPrinter: parseResult.GetValueForOption(_installEveryonesPrinterOption),
                installAdobeReader: parseResult.GetValueForOption(_installAdobeReaderOption),
                installHancomOfficeViewer: parseResult.GetValueForOption(_installHancomOfficeViewerOption),
                installRaiDrive: parseResult.GetValueForOption(_installRaiDriveOption),
                enableInternetExplorerMode: parseResult.GetValueForOption(_enableIEModeOption),
                showCommandLineHelp: parseResult.HasOption(_helpOption),
                showVersionHelp: parseResult.HasOption(_versionOption),
                dryRun: parseResult.GetValueForOption(_dryRunOption),
                simulateFailure: parseResult.GetValueForOption(_simulateFailureOption));
        }
    }
}
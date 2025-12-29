using System;
using System.CommandLine;
using System.CommandLine.Help;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Models;
using TableCloth.Resources;

namespace Spork.Components.Implementations
{
    public sealed class CommandLineArguments : ICommandLineArguments
    {
        public CommandLineArguments()
        {
            _certPrivateKeyOption = new Option<string>(ConstantStrings.TableCloth_Switch_CertPrivateKey)
            { Required = false, Arity = ArgumentArity.ExactlyOne, Description = UIStringResources.TableCloth_Switch_CertPrivateKey_Help, };

            _certPublicKeyOption = new Option<string>(ConstantStrings.TableCloth_Switch_CertPublicKey)
            { Required = false, Arity = ArgumentArity.ExactlyOne, Description = UIStringResources.TableCloth_Switch_CertPublicKey_Help, };

            _installEveryonesPrinterOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_InstallEveryonesPrinter)
            { Required = false, Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_InstallEveryonesPrinter_Help, };

            _installAdobeReaderOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_InstallAdobeReader)
            { Required = false, Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_InstallAdobeReader_Help, };

            _installHancomOfficeViewerOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_InstallHancomOfficeViewer)
            { Required = false, Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_InstallHancomOfficeViewer_Help, };

            _installRaiDriveOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_InstallRaiDrive)
            { Required = false, Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_InstallRaiDrive_Help, };

            _dryRunOption = new Option<bool>(ConstantStrings.TableCloth_Switch_DryRun)
            { Required = false, Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_DryRun_Help, };

            _simulateFailureOption = new Option<bool>(ConstantStrings.TableCloth_Switch_SimulateFailure)
            { Required = false, Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_SimulateFailure_Help, };

            _siteIdListArgument = new Argument<string[]>("siteIds")
            { Arity = ArgumentArity.ZeroOrMore, Description = UIStringResources.TableCloth_Arguments_SiteIdList_Help, };

            _rootCommand = new RootCommand()
            {
                _certPrivateKeyOption,
                _certPublicKeyOption,
                _installEveryonesPrinterOption,
                _installAdobeReaderOption,
                _installHancomOfficeViewerOption,
                _installRaiDriveOption,
                _dryRunOption,
                _simulateFailureOption,
                _siteIdListArgument,
            };

            _helpOption = _rootCommand.Options
                .FirstOrDefault(x => x is HelpOption) as HelpOption
                ?? throw new Exception(ErrorStrings.Error_HelpSwitch_NotFound);

            _versionOption = _rootCommand.Options
                .FirstOrDefault(x => x is VersionOption) as VersionOption
                ?? throw new Exception(ErrorStrings.Error_VersionSwitch_NotFound);
        }

        private readonly Option<string> _certPrivateKeyOption;
        private readonly Option<string> _certPublicKeyOption;
        private readonly Option<bool?> _installEveryonesPrinterOption;
        private readonly Option<bool?> _installAdobeReaderOption;
        private readonly Option<bool?> _installHancomOfficeViewerOption;
        private readonly Option<bool?> _installRaiDriveOption;
        private readonly Option<bool> _dryRunOption;
        private readonly Option<bool> _simulateFailureOption;
        private readonly Argument<string[]> _siteIdListArgument;
        private readonly RootCommand _rootCommand;
        private readonly HelpOption _helpOption;
        private readonly VersionOption _versionOption;

        private ParseResult ParseCommandLine(string[] args)
            => _rootCommand.Parse(args);

        public async Task<string> GetHelpStringAsync()
        {
            var output = new StringWriter();
            var parseResult = ParseCommandLine(new string[] { ConstantStrings.TableCloth_Switch_Help });
            await parseResult.InvokeAsync(new InvocationConfiguration { Output = output });
            return output.ToString() ?? string.Empty;
        }

        public async Task<string> GetVersionStringAsync()
        {
            var output = new StringWriter();
            var parseResult = ParseCommandLine(new string[] { ConstantStrings.TableCloth_Switch_Version });
            await parseResult.InvokeAsync(new InvocationConfiguration { Output = output });
            return output.ToString() ?? string.Empty;
        }

        public CommandLineArgumentModel GetCurrent()
        {
            var args = Helpers.GetCommandLineArguments();
            var parseResult = ParseCommandLine(args);

            return new CommandLineArgumentModel(args,
                selectedServices: parseResult.GetValue(_siteIdListArgument),
                enableMicrophone: default,
                enableWebCam: default,
                enablePrinters: default,
                certPrivateKeyPath: parseResult.GetValue(_certPrivateKeyOption),
                certPublicKeyPath: parseResult.GetValue(_certPublicKeyOption),
                installEveryonesPrinter: parseResult.GetValue(_installEveryonesPrinterOption),
                installAdobeReader: parseResult.GetValue(_installAdobeReaderOption),
                installHancomOfficeViewer: parseResult.GetValue(_installHancomOfficeViewerOption),
                installRaiDrive: parseResult.GetValue(_installRaiDriveOption),
                showCommandLineHelp: parseResult.GetResult(_helpOption) != null,
                showVersionHelp: parseResult.GetResult(_versionOption) != null,
                dryRun: parseResult.GetValue(_dryRunOption),
                simulateFailure: parseResult.GetValue(_simulateFailureOption));
        }
    }
}

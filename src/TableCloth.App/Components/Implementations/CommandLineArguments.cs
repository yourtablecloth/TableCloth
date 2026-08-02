using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TableCloth.Models;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class CommandLineArguments : ICommandLineArguments
{
    public CommandLineArguments()
    {
        _enableMicrophoneOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_EnableMicrophone)
        { Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_EnableMicrophone_Help, };

        _enableCameraOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_EnableCamera)
        { Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_EnableCamera_Help, };

        _enablePrinterOption = new Option<bool?>(ConstantStrings.TableCloth_Switch_EnablePrinter)
        { Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_EnablePrinter_Help, };

        _certPrivateKeyOption = new Option<string>(ConstantStrings.TableCloth_Switch_CertPrivateKey)
        { Arity = ArgumentArity.ExactlyOne, Description = UIStringResources.TableCloth_Switch_CertPrivateKey_Help, };

        _certPublicKeyOption = new Option<string>(ConstantStrings.TableCloth_Switch_CertPublicKey)
        { Arity = ArgumentArity.ExactlyOne, Description = UIStringResources.TableCloth_Switch_CertPublicKey_Help, };

        _dryRunOption = new Option<bool>(ConstantStrings.TableCloth_Switch_DryRun)
        { Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_DryRun_Help, };

        _simulateFailureOption = new Option<bool>(ConstantStrings.TableCloth_Switch_SimulateFailure)
        { Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_SimulateFailure_Help, };

        // 딥링크가 지정한 대상 페이지. 신뢰할 수 없는 입력이므로 여기서는 문자열로만 받고,
        // 카탈로그 도메인 판정은 CatalogTargetUrlMatcher 가 한다.
        _targetUrlOption = new Option<string>(ConstantStrings.TableCloth_Switch_TargetUrl)
        { Arity = ArgumentArity.ExactlyOne, Description = UIStringResources.TableCloth_Switch_TargetUrl_Help, };

        // `tablecloth:` 스킴 처리기가 넘기는 원문. 진입점(Program)이 파싱해 정규 인자로 바꾸므로
        // 여기까지 값이 도달하지는 않지만, --help 에 노출하기 위해 등록해 둔다.
        _uriOption = new Option<string>(ConstantStrings.TableCloth_Switch_Uri)
        { Arity = ArgumentArity.ExactlyOne, Description = UIStringResources.TableCloth_Switch_Uri_Help, };

        // 딥링크 진입 표식 — 상세 화면 없이 곧바로 샌드박스를 실행한다.
        _launchOption = new Option<bool>(ConstantStrings.TableCloth_Switch_Launch)
        { Arity = ArgumentArity.Zero, Description = UIStringResources.TableCloth_Switch_Launch_Help, };

        _siteIdListArgument = new Argument<string[]>("siteIds")
        { Arity = ArgumentArity.ZeroOrMore, Description = UIStringResources.TableCloth_Arguments_SiteIdList_Help, };

        _rootCommand = new RootCommand()
        {
            _targetUrlOption,
            _uriOption,
            _launchOption,
            _enableMicrophoneOption,
            _enableCameraOption,
            _enablePrinterOption,
            _certPrivateKeyOption,
            _certPublicKeyOption,
            _dryRunOption,
            _simulateFailureOption,
            _siteIdListArgument,
        };

        _helpOption = _rootCommand.Options
            .FirstOrDefault(x => x.Name.Equals(ConstantStrings.TableCloth_Switch_Help, StringComparison.OrdinalIgnoreCase)
                || x.Aliases.Contains(ConstantStrings.TableCloth_Switch_Help, StringComparer.OrdinalIgnoreCase))
            ?? throw new Exception(ErrorStrings.Error_HelpSwitch_NotFound);

        _versionOption = _rootCommand.Options
            .FirstOrDefault(x => x.Name.Equals(ConstantStrings.TableCloth_Switch_Version, StringComparison.OrdinalIgnoreCase)
                || x.Aliases.Contains(ConstantStrings.TableCloth_Switch_Version, StringComparer.OrdinalIgnoreCase))
            ?? throw new Exception(ErrorStrings.Error_VersionSwitch_NotFound);
    }

    private readonly Option<bool?> _enableMicrophoneOption;
    private readonly Option<bool?> _enableCameraOption;
    private readonly Option<bool?> _enablePrinterOption;
    private readonly Option<string> _certPrivateKeyOption;
    private readonly Option<string> _certPublicKeyOption;
    private readonly Option<bool> _dryRunOption;
    private readonly Option<bool> _simulateFailureOption;
    private readonly Option<string> _targetUrlOption;
    private readonly Option<string> _uriOption;
    private readonly Option<bool> _launchOption;
    private readonly Argument<string[]> _siteIdListArgument;
    private readonly RootCommand _rootCommand;
    private readonly Option _helpOption;
    private readonly Option _versionOption;

    private ParseResult ParseCommandLine(string[] args)
        => _rootCommand.Parse(args);

    public async Task<string> GetHelpStringAsync()
    {
        var output = new StringWriter();
        var config = new InvocationConfiguration()
        {
            Output = output,
        };
        var parseResult = ParseCommandLine([ConstantStrings.TableCloth_Switch_Help]);
        await parseResult.InvokeAsync(config).ConfigureAwait(false);
        return output.ToString();
    }

    public async Task<string> GetVersionStringAsync()
    {
        var output = new StringWriter();
        var config = new InvocationConfiguration()
        {
            Output = output,
        };
        var parseResult = ParseCommandLine([ConstantStrings.TableCloth_Switch_Version]);
        await parseResult.InvokeAsync(config).ConfigureAwait(false);
        return output.ToString();
    }

    public CommandLineArgumentModel GetCurrent()
    {
        var args = Helpers.GetCommandLineArguments();
        var parseResult = ParseCommandLine(args);

        return new CommandLineArgumentModel(args,
            selectedServices: parseResult.GetValue(_siteIdListArgument),
            enableMicrophone: parseResult.GetValue(_enableMicrophoneOption),
            enableWebCam: parseResult.GetValue(_enableCameraOption),
            enablePrinters: parseResult.GetValue(_enablePrinterOption),
            certPrivateKeyPath: parseResult.GetValue(_certPrivateKeyOption),
            certPublicKeyPath: parseResult.GetValue(_certPublicKeyOption),
            showCommandLineHelp: parseResult.GetResult(_helpOption) != null,
            showVersionHelp: parseResult.GetResult(_versionOption) != null,
            dryRun: parseResult.GetValue(_dryRunOption),
            simulateFailure: parseResult.GetValue(_simulateFailureOption),
            targetUrl: parseResult.GetValue(_targetUrlOption),
            launchImmediately: parseResult.GetValue(_launchOption));
    }
}

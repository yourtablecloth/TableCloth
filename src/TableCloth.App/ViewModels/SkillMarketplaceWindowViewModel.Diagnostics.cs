using System;
using System.Threading.Tasks;
using TableCloth.ManagedAi;

namespace TableCloth.ViewModels;

public partial class SkillMarketplaceWindowViewModel
{
    private readonly IManagedAiSkillRuntimeDiagnostics? _skillRuntimeDiagnostics;
    private SkillRuntimeDiagnosticsResult? _skillRuntimeDiagnosticsResult;

    public string WindowsLongPathStatus => _skillRuntimeDiagnosticsResult?.WindowsPolicy switch
    {
        WindowsLongPathPolicy.Enabled => ManagedAiText.Select("사용", "Enabled"),
        WindowsLongPathPolicy.Disabled => ManagedAiText.Select("사용 안 함", "Disabled"),
        WindowsLongPathPolicy.Unsupported => ManagedAiText.Select("이 Windows에서 지원하지 않음", "Unsupported on this Windows version"),
        WindowsLongPathPolicy.Unknown => ManagedAiText.Select("확인 불가", "Unknown"),
        _ => ManagedAiText.Select("검사 전", "Not checked")
    };
    public string ManagedLongPathStatus => LongPathStatusLabel(_skillRuntimeDiagnosticsResult?.ManagedIo);
    public string NodeJsLongPathStatus => LongPathStatusLabel(_skillRuntimeDiagnosticsResult?.NodeJs);
    public string PythonLongPathStatus => LongPathStatusLabel(_skillRuntimeDiagnosticsResult?.Python);
    public bool HasSkillRuntimeLimits => _skillRuntimeDiagnosticsResult is { } result
        && (result.WindowsPolicy is WindowsLongPathPolicy.Disabled or WindowsLongPathPolicy.Unsupported
            || result.ManagedIo == LongPathSupport.Limited || result.NodeJs == LongPathSupport.Limited || result.Python == LongPathSupport.Limited);
    public bool HasSkillRuntimeUnknown => _skillRuntimeDiagnosticsResult is { } result
        && (result.WindowsPolicy == WindowsLongPathPolicy.Unknown || result.ManagedIo == LongPathSupport.Unknown
            || result.NodeJs == LongPathSupport.Unknown || result.Python == LongPathSupport.Unknown);
    public string SkillRuntimeUnknownDescription => _skillRuntimeDiagnostics is null
        ? ManagedAiText.Select("환경 검사 서비스를 사용할 수 없어 결과를 확인할 수 없습니다.", "Environment checks are unavailable, so their results are unknown.")
        : ManagedAiText.Select("일부 검사 결과를 확인할 수 없습니다. 설치 및 환경 검사로 다시 확인할 수 있습니다.",
            "Some check results are unknown. Run the installation and environment check again to retry.");

    private static string LongPathStatusLabel(LongPathSupport? support) => support switch
    {
        LongPathSupport.Supported => ManagedAiText.Select("긴 경로 검사 통과", "Long-path check passed"),
        LongPathSupport.Limited => ManagedAiText.Select("긴 경로 사용 제한", "Long-path limited"),
        LongPathSupport.NotInstalled => ManagedAiText.Select("설치 안 함", "Not installed"),
        LongPathSupport.Unknown => ManagedAiText.Select("확인 불가", "Unknown"),
        _ => ManagedAiText.Select("검사 전", "Not checked")
    };

    private async Task CheckSkillRuntimeDiagnosticsAsync()
    {
        if (_skillRuntimeDiagnostics is null) { SetUnknownSkillRuntimeDiagnostics(); return; }
        try
        {
            _skillRuntimeDiagnosticsResult = await Task.Run(() => _skillRuntimeDiagnostics.CheckAsync(_token), _token);
            NotifySkillRuntimeDiagnostics();
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch { SetUnknownSkillRuntimeDiagnostics(); }
    }

    private void SetUnknownSkillRuntimeDiagnostics()
    {
        _skillRuntimeDiagnosticsResult = new(WindowsLongPathPolicy.Unknown, LongPathSupport.Unknown, LongPathSupport.Unknown, LongPathSupport.Unknown);
        NotifySkillRuntimeDiagnostics();
    }

    private void NotifySkillRuntimeDiagnostics()
    {
        OnPropertyChanged(nameof(WindowsLongPathStatus));
        OnPropertyChanged(nameof(ManagedLongPathStatus));
        OnPropertyChanged(nameof(NodeJsLongPathStatus));
        OnPropertyChanged(nameof(PythonLongPathStatus));
        OnPropertyChanged(nameof(HasSkillRuntimeLimits));
        OnPropertyChanged(nameof(HasSkillRuntimeUnknown));
    }
}

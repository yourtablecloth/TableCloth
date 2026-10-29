using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using TableCloth.ManagedAi;

namespace TableCloth.ViewModels;

public partial class SkillMarketplaceWindowViewModel
{
    private readonly IManagedAiScriptRuntimeManager? _scriptRuntimes;
    private AiScriptRuntimeStatus? _scriptRuntimeStatus;

    [ObservableProperty] private bool _skillRuntimeBusy;
    [ObservableProperty] private string _skillRuntimeNotice = string.Empty;

    public string PythonRuntimeVersion => RuntimeVersionLabel(_scriptRuntimeStatus?.Python);
    public string NodeJsRuntimeVersion => RuntimeVersionLabel(_scriptRuntimeStatus?.Node);
    public string PythonLongPathStatus => LongPathLabel(_scriptRuntimeStatus?.LongPaths.PythonSupported);
    public string NodeJsLongPathStatus => LongPathLabel(_scriptRuntimeStatus?.LongPaths.NodeSupported);
    public bool HasSkillRuntimeLimits => _scriptRuntimeStatus?.LongPaths is { } result
        && (result.PythonSupported == false || result.NodeSupported == false);
    public bool HasSkillRuntimeUnknown => _scriptRuntimeStatus?.LongPaths is not { } result
        || result.PythonSupported is null || result.NodeSupported is null;
    public bool HasSkillRuntimeWarning => HasSkillRuntimeLimits || HasSkillRuntimeUnknown;
    public string SkillRuntimeWarning => HasSkillRuntimeLimits
        ? ManagedAiText.Select("260자를 넘는 경로 검사에서 제한을 확인했습니다. 긴 경로를 사용하는 일부 스킬의 기능이 제한될 수 있습니다.",
            "The check found a limit on paths longer than 260 characters. Some skill features that use long paths may be unavailable.")
        : ManagedAiText.Select("260자를 넘는 경로 지원을 확인하지 못했습니다. 긴 경로를 사용하는 일부 스킬의 기능이 제한될 수 있습니다. 실행 환경 설치나 복구 후 다시 검사할 수 있습니다.",
            "Support for paths longer than 260 characters could not be verified. Some skill features that use long paths may be unavailable. Install or repair the runtimes and check again.");

    private static string RuntimeVersionLabel(AiScriptRuntimeComponentStatus? status) => status switch
    {
        { Installed: true, InstalledVersion: { Length: > 0 } version } => version,
        { Installed: false, Failure: "RuntimeNotInstalled" } => ManagedAiText.Select("설치 안 함", "Not installed"),
        { Failure: { Length: > 0 } } => ManagedAiText.Select("확인 불가", "Could not verify"),
        { Installed: false } => ManagedAiText.Select("설치 안 함", "Not installed"),
        _ => ManagedAiText.Select("확인 전", "Not checked")
    };

    private static string LongPathLabel(bool? supported) => supported switch
    {
        true => ManagedAiText.Select("검사 통과", "Passed"),
        false => ManagedAiText.Select("사용 제한", "Limited"),
        null => ManagedAiText.Select("확인 불가", "Unverified")
    };

    partial void OnSkillRuntimeBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusy));
        InstallSkillRuntimesCommand.NotifyCanExecuteChanged();
        RefreshSkillRuntimeStatusCommand.NotifyCanExecuteChanged();
    }

    private bool CanInstallSkillRuntimes() => _scriptRuntimes is not null && CanUseLocal() && !SkillRuntimeBusy;
    private bool CanRefreshSkillRuntimeStatus() => _scriptRuntimes is not null && !_disposed && !SkillRuntimeBusy;

    [RelayCommand(CanExecute = nameof(CanRefreshSkillRuntimeStatus))]
    private Task RefreshSkillRuntimeStatus() => RunSkillRuntimeOperationAsync(false);

    [RelayCommand(CanExecute = nameof(CanInstallSkillRuntimes))]
    private Task InstallSkillRuntimes() => RunSkillRuntimeOperationAsync(true);

    private async Task RunSkillRuntimeOperationAsync(bool install)
    {
        if (install ? !CanInstallSkillRuntimes() : !CanRefreshSkillRuntimeStatus()) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (install)
        {
            _localCompletion = completion;
            LocalBusy = true;
        }
        SkillRuntimeBusy = true;
        _scriptRuntimeStatus = null;
        NotifyScriptRuntimeStatus();
        SkillRuntimeNotice = install
            ? ManagedAiText.Select("전용 Python과 Node.js를 설치하거나 복구하고 있습니다.", "Installing or repairing dedicated Python and Node.js.")
            : ManagedAiText.Select("전용 실행 환경과 격리 환경 안에서의 긴 경로 지원을 검사하고 있습니다.", "Checking dedicated runtimes and long-path support inside the isolated environment.");
        try
        {
            _scriptRuntimeStatus = await Task.Run(() => install
                ? _scriptRuntimes!.EnsureInstalledAsync(_token)
                : _scriptRuntimes!.GetStatusAsync(_token), _token);
            NotifyScriptRuntimeStatus();
            SkillRuntimeNotice = _scriptRuntimeStatus.Python.Installed && _scriptRuntimeStatus.Node.Installed
                ? ManagedAiText.Select("전용 실행 환경 검사를 마쳤습니다.", "Dedicated runtime checks completed.")
                : ManagedAiText.Select("실행 환경 설치 또는 복구 후 다시 검사할 수 있습니다.", "Install or repair the runtimes to check again.");
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (OperationCanceledException)
        {
            SkillRuntimeNotice = ManagedAiText.Select("실행 환경 작업을 취소했습니다.", "Runtime operation canceled.");
        }
        catch
        {
            SkillRuntimeNotice = ManagedAiText.Select("전용 실행 환경을 확인하지 못했습니다. 설치 또는 복구 후 다시 시도할 수 있습니다.",
                "Could not verify the dedicated runtimes. Install or repair them and try again.");
        }
        finally
        {
            SkillRuntimeBusy = false;
            if (install) LocalBusy = false;
            completion.TrySetResult();
        }
    }

    private void NotifyScriptRuntimeStatus()
    {
        OnPropertyChanged(nameof(PythonRuntimeVersion));
        OnPropertyChanged(nameof(NodeJsRuntimeVersion));
        OnPropertyChanged(nameof(PythonLongPathStatus));
        OnPropertyChanged(nameof(NodeJsLongPathStatus));
        OnPropertyChanged(nameof(HasSkillRuntimeLimits));
        OnPropertyChanged(nameof(HasSkillRuntimeUnknown));
        OnPropertyChanged(nameof(HasSkillRuntimeWarning));
        OnPropertyChanged(nameof(SkillRuntimeWarning));
    }
}

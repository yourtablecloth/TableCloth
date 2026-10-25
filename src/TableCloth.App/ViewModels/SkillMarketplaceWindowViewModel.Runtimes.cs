using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using TableCloth.ManagedAi;

namespace TableCloth.ViewModels;

public partial class SkillMarketplaceWindowViewModel
{
    private readonly IManagedAiSkillRuntimeManager? _skillRuntimes;
    private SkillRuntimeEnvironment? _skillRuntimeEnvironment;
    private bool _skillRuntimeStatusKnown;
    private TaskCompletionSource? _skillRuntimeStatusCompletion;

    [ObservableProperty] private bool _skillRuntimeStatusBusy;
    [ObservableProperty] private bool _skillRuntimeInstalling;
    [ObservableProperty] private string _skillRuntimeNotice = string.Empty;

    public bool SkillRuntimeBusy => SkillRuntimeStatusBusy || SkillRuntimeInstalling;
    public string NodeJsRuntimeVersion => RuntimeVersionLabel(_skillRuntimeEnvironment?.NodeJs.Version);
    public string PythonRuntimeVersion => RuntimeVersionLabel(_skillRuntimeEnvironment?.Python.Version);

    private string RuntimeVersionLabel(string? version) => version is not null ? version
        : _skillRuntimeStatusKnown ? ManagedAiText.Select("설치 안 함", "Not installed")
        : ManagedAiText.Select("확인 전", "Not checked");

    partial void OnSkillRuntimeStatusBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(SkillRuntimeBusy));
        InstallSkillRuntimesCommand.NotifyCanExecuteChanged();
        RefreshSkillRuntimeStatusCommand.NotifyCanExecuteChanged();
    }

    partial void OnSkillRuntimeInstallingChanged(bool value)
    {
        OnPropertyChanged(nameof(SkillRuntimeBusy));
        RefreshSkillRuntimeStatusCommand.NotifyCanExecuteChanged();
    }

    private void NotifySkillRuntimeVersions()
    {
        OnPropertyChanged(nameof(NodeJsRuntimeVersion));
        OnPropertyChanged(nameof(PythonRuntimeVersion));
    }

    private bool CanInstallSkillRuntimes() => _skillRuntimes is not null && CanUseLocal() && !SkillRuntimeStatusBusy;
    private bool CanRefreshSkillRuntimeStatus() => _skillRuntimes is not null && !_disposed
        && !SkillRuntimeStatusBusy && !SkillRuntimeInstalling;

    [RelayCommand(CanExecute = nameof(CanRefreshSkillRuntimeStatus))]
    private async Task RefreshSkillRuntimeStatus()
    {
        if (!CanRefreshSkillRuntimeStatus()) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _skillRuntimeStatusCompletion = completion;
        SkillRuntimeStatusBusy = true;
        SkillRuntimeNotice = ManagedAiText.Select("전용 실행 환경의 설치 상태와 긴 경로 지원을 검사하고 있습니다.", "Checking the dedicated runtime installation and long-path support.");
        try
        {
            var installation = ReadSkillRuntimeInstallationAsync();
            await Task.WhenAll(installation, CheckSkillRuntimeDiagnosticsAsync());
            if (await installation)
                SkillRuntimeNotice = _skillRuntimeEnvironment is null
                    ? ManagedAiText.Select("전용 Node.js와 Python을 아직 설치하지 않았습니다.", "Dedicated Node.js and Python are not installed yet.")
                    : ManagedAiText.Select("전용 Node.js와 Python의 설치 버전을 확인했습니다.", "Dedicated Node.js and Python versions checked.");
        }
        finally { SkillRuntimeStatusBusy = false; completion.TrySetResult(); }
    }

    private async Task<bool> ReadSkillRuntimeInstallationAsync()
    {
        try
        {
            _skillRuntimeEnvironment = await Task.Run(() => _skillRuntimes!.GetActiveAsync(_token), _token);
            _skillRuntimeStatusKnown = true;
            NotifySkillRuntimeVersions();
            return true;
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { return false; }
        catch
        {
            SkillRuntimeNotice = ManagedAiText.Select("전용 실행 환경의 설치 상태를 확인하지 못했습니다.", "Could not check the dedicated runtime installation.");
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInstallSkillRuntimes))]
    private async Task InstallSkillRuntimes()
    {
        if (!CanInstallSkillRuntimes()) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _localCompletion = completion;
        LocalBusy = true;
        SkillRuntimeInstalling = true;
        SkillRuntimeNotice = ManagedAiText.Select("전용 Node.js와 Python을 설치하고 있습니다.", "Installing dedicated Node.js and Python.");
        var progress = new Progress<AiProgress>(update =>
        {
            if (_disposed || !SkillRuntimeInstalling || !ReferenceEquals(_localCompletion, completion)) return;
            SkillRuntimeNotice = update.Stage switch
            {
                "SkillRuntimeDownloadingNode" => ManagedAiText.Select("Node.js 설치 파일을 내려받고 있습니다.", "Downloading the Node.js package."),
                "SkillRuntimeDownloadingPython" => ManagedAiText.Select("Python 설치 파일을 내려받고 있습니다.", "Downloading the Python package."),
                "SkillRuntimeExtracting" => ManagedAiText.Select("전용 실행 환경의 압축을 풀고 있습니다.", "Extracting the dedicated runtimes."),
                "SkillRuntimeVerifying" => ManagedAiText.Select("전용 실행 환경의 파일과 버전을 검증하고 있습니다.", "Verifying dedicated runtime files and versions."),
                "SkillRuntimeActivating" => ManagedAiText.Select("전용 실행 환경의 설치를 마무리하고 있습니다.", "Completing the dedicated runtime installation."),
                _ => ManagedAiText.Select("전용 Node.js와 Python을 설치하고 있습니다.", "Installing dedicated Node.js and Python.")
            };
        });
        try
        {
            _skillRuntimeEnvironment = await Task.Run(() => _skillRuntimes!.InstallAsync(progress, _token), _token);
            _skillRuntimeStatusKnown = true;
            NotifySkillRuntimeVersions();
            await CheckSkillRuntimeDiagnosticsAsync();
            SkillRuntimeNotice = ManagedAiText.Select("전용 Node.js와 Python을 설치했습니다.", "Dedicated Node.js and Python installed.");
        }
        catch (OperationCanceledException) when (_token.IsCancellationRequested) { }
        catch (OperationCanceledException) { SkillRuntimeNotice = ManagedAiText.Select("실행 환경 설치를 취소했습니다.", "Runtime installation canceled."); }
        catch (ManagedAiException error)
        {
            SkillRuntimeNotice = error.Code switch
            {
                AiFailureCode.RuntimeBusy => ManagedAiText.Select("다른 창에서 AI 작업을 진행하고 있습니다. 잠시 후 다시 시도할 수 있습니다.", "Another window is using AI. Try again shortly."),
                AiFailureCode.UnsupportedArchitecture => ManagedAiText.Select("현재 컴퓨터의 아키텍처에 맞는 실행 환경을 설치할 수 없습니다.", "Dedicated runtimes are unavailable for this computer architecture."),
                AiFailureCode.OfficialMetadataUnavailable => ManagedAiText.Select("실행 환경 설치 파일을 내려받지 못했습니다. 연결 상태를 확인한 뒤 다시 시도할 수 있습니다.", "Could not download the runtime packages. Check the connection and try again."),
                AiFailureCode.IntegrityMismatch or AiFailureCode.ArchiveRejected or AiFailureCode.RuntimeVersionMismatch => ManagedAiText.Select("실행 환경 설치 파일이 검증을 통과하지 못했습니다.", "The runtime packages failed verification."),
                AiFailureCode.InvalidPath => ManagedAiText.Select("전용 실행 환경 폴더에 접근하지 못했습니다.", "Could not access the dedicated runtime folder."),
                AiFailureCode.PathLengthUnsupported => ManagedAiText.Select("실행 환경 설치 경로가 현재 환경의 길이 제한을 넘었습니다.", "The runtime installation path exceeds the path-length limit in this environment."),
                _ => ManagedAiText.Select("전용 실행 환경을 설치하지 못했습니다. 잠시 후 다시 시도할 수 있습니다.", "Could not install the dedicated runtimes. Try again shortly.")
            };
        }
        catch { SkillRuntimeNotice = ManagedAiText.Select("전용 실행 환경을 설치하지 못했습니다. 잠시 후 다시 시도할 수 있습니다.", "Could not install the dedicated runtimes. Try again shortly."); }
        finally
        {
            SkillRuntimeInstalling = false;
            LocalBusy = false;
            completion.TrySetResult();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Commands.SplashScreen;
using TableCloth.Events;
using TableCloth.Models;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class SplashScreenViewModelForDesigner : SplashScreenViewModel { }

public class SplashScreenViewModel : ViewModelBase
{
    protected SplashScreenViewModel() { }

    public SplashScreenViewModel(
        SplashScreenLoadedCommand splashScreenLoadedCommand)
    {
        _splashScreenLoadedCommand = splashScreenLoadedCommand;
    }

    public event EventHandler<StatusUpdateRequestEventArgs>? StatusUpdate;
    public event EventHandler<DialogRequestEventArgs>? InitializeDone;

    public async Task NotifyStatusUpdateAsync(object sender, StatusUpdateRequestEventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => StatusUpdate?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task NotifyInitializedAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => InitializeDone?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    private readonly SplashScreenLoadedCommand _splashScreenLoadedCommand = default!;

    public SplashScreenLoadedCommand SplashScreenLoadedCommand
        => _splashScreenLoadedCommand;

    private string _appVersion = Helpers.GetAppVersion();
    private string _status = UIStringResources.Status_PleaseWait;
    private bool _appStartupSucceed = false;
    private IList<string> _passedArguments = Array.Empty<string>();
    private CommandLineArgumentModel? _parsedArgument;
    private IList<string> _warnings = new List<string>();
    private bool _isUpdating = false;
    private int _updateProgress = 0;
    private bool _showUpdateProgress = false;

    public string AppVersion
    {
        get => _appVersion;
        set => SetProperty(ref _appVersion, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool AppStartupSucceed
    {
        get => _appStartupSucceed;
        set => SetProperty(ref _appStartupSucceed, value);
    }

    public IList<string> PassedArguments
    {
        get => _passedArguments;
        set => SetProperty(ref _passedArguments, value);
    }

    public CommandLineArgumentModel? ParsedArgument
    {
        get => _parsedArgument;
        set => SetProperty(ref _parsedArgument, value);
    }

    public IList<string> Warnings
    {
        get => _warnings;
        set => SetProperty(ref _warnings, value);
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        set => SetProperty(ref _isUpdating, value);
    }

    public int UpdateProgress
    {
        get => _updateProgress;
        set => SetProperty(ref _updateProgress, value);
    }

    public bool ShowUpdateProgress
    {
        get => _showUpdateProgress;
        set => SetProperty(ref _showUpdateProgress, value);
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
public partial class SplashScreenViewModelForDesigner : SplashScreenViewModel { }

public partial class SplashScreenViewModel : ViewModelBase
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

    [RelayCommand]
    private void SplashScreenLoaded()
    {
        _splashScreenLoadedCommand.Execute(this);
    }

    private SplashScreenLoadedCommand _splashScreenLoadedCommand = default!;

    [ObservableProperty]
    private string _appVersion = Helpers.GetAppVersion();

    [ObservableProperty]
    private string _status = UIStringResources.Status_PleaseWait;

    [ObservableProperty]
    private bool _appStartupSucceed = false;

    [ObservableProperty]
    private IList<string> _passedArguments = Array.Empty<string>();

    [ObservableProperty]
    private CommandLineArgumentModel? _parsedArgument;

    [ObservableProperty]
    private IList<string> _warnings = new List<string>();

    [ObservableProperty]
    private bool _isUpdating = false;

    [ObservableProperty]
    private int _updateProgress = 0;

    [ObservableProperty]
    private bool _showUpdateProgress = false;
}

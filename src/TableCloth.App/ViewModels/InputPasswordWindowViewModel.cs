using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class InputPasswordWindowViewModelForDesigner : InputPasswordWindowViewModel { }

public partial class InputPasswordWindowViewModel : ObservableObject
{
    protected InputPasswordWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
    public InputPasswordWindowViewModel(
        IX509CertPairScanner certPairScanner,
        IAppMessageBox appMessageBox,
        TaskFactory taskFactory)
    {
        _certPairScanner = certPairScanner;
        _appMessageBox = appMessageBox;
    }

    public event EventHandler? ViewLoaded;
    public event EventHandler<DialogRequestEventArgs>? CloseRequested;
    public event EventHandler? RetryPasswordInputRequested;

    public async Task NotifyViewLoadedAsync(object? sender, EventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => ViewLoaded?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task RequestRetryPasswordInputAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => RetryPasswordInputRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    [RelayCommand]
    private async Task InputPasswordWindowConfirm()
    {
        try
        {
            var pfxFilePath = PfxFilePath.EnsureNotNull(ErrorStrings.Error_Cannot_Find_PfxFile);
            var certPair = _certPairScanner.CreateX509Cert(pfxFilePath, Password);

            if (certPair != null)
                ValidatedCertPair = certPair;

            await RequestCloseAsync(this, new DialogRequestEventArgs(true));
        }
        catch (Exception ex)
        {
            _appMessageBox.DisplayError(ex, false);
        }
    }

    [RelayCommand]
    private async Task InputPasswordWindowCancel()
    {
        ValidatedCertPair = null;
        await RequestCloseAsync(this, new DialogRequestEventArgs(false));
    }

    [RelayCommand]
    private async Task InputPasswordWindowLoaded()
    {
        await NotifyViewLoadedAsync(this, EventArgs.Empty);
    }

    [ObservableProperty]
    private string _pfxFilePath = string.Empty;

    [ObservableProperty]
    private SecureString _password = new();

    [ObservableProperty]
    private X509CertPair? _validatedCertPair = null;

    private readonly IX509CertPairScanner _certPairScanner = default!;
    private readonly IAppMessageBox _appMessageBox = default!;
    private readonly TaskFactory _taskFactory = default!;
}

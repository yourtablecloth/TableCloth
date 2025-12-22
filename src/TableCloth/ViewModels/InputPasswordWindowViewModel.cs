using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Commands.InputPasswordWindow;
using TableCloth.Events;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class InputPasswordWindowViewModelForDesigner : InputPasswordWindowViewModel { }

public partial class InputPasswordWindowViewModel : ViewModelBase
{
    protected InputPasswordWindowViewModel() { }

    public InputPasswordWindowViewModel(
        InputPasswordWindowLoadedCommand inputPasswordWindowLoadedCommand,
        InputPasswordWindowConfirmCommand inputPasswordWindowConfirmCommand,
        InputPasswordWindowCancelCommand inputPasswordWindowCancelCommand)
    {
        _inputPasswordWindowLoadedCommand = inputPasswordWindowLoadedCommand;
        _inputPasswordWindowConfirmCommand = inputPasswordWindowConfirmCommand;
        _inputPasswordWindowCancelCommand = inputPasswordWindowCancelCommand;
    }

    public event EventHandler? ViewLoaded;
    public event EventHandler<DialogRequestEventArgs>? CloseRequested;
    public event EventHandler? RetryPasswordInputRequested;

    public async Task NotifyViewLoadedAsync(object? sender, EventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => ViewLoaded?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task RequestRetryPasswordInputAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => RetryPasswordInputRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    [ObservableProperty]
    private InputPasswordWindowLoadedCommand _inputPasswordWindowLoadedCommand = default!;

    [ObservableProperty]
    private InputPasswordWindowConfirmCommand _inputPasswordWindowConfirmCommand = default!;

    [ObservableProperty]
    private InputPasswordWindowCancelCommand _inputPasswordWindowCancelCommand = default!;

    [ObservableProperty]
    private string _pfxFilePath = string.Empty;

    [ObservableProperty]
    private SecureString _password = new();

    [ObservableProperty]
    private X509CertPair? _validatedCertPair = null;
}

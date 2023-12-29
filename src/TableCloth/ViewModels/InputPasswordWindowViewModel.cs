using System;
using System.Security;
using TableCloth.Commands.InputPasswordWindow;
using TableCloth.Events;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class InputPasswordWindowViewModelForDesigner : InputPasswordWindowViewModel { }

public class InputPasswordWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected InputPasswordWindowViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

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

    public void NotifyViewLoaded(object? sender, EventArgs e)
        => ViewLoaded?.Invoke(sender, e);

    public void RequestClose(object sender, DialogRequestEventArgs e)
        => CloseRequested?.Invoke(sender, e);

    public void RequestRetryPasswordInput(object sender, EventArgs e)
        => RetryPasswordInputRequested?.Invoke(sender, e);

    private readonly InputPasswordWindowLoadedCommand _inputPasswordWindowLoadedCommand;
    private readonly InputPasswordWindowConfirmCommand _inputPasswordWindowConfirmCommand;
    private readonly InputPasswordWindowCancelCommand _inputPasswordWindowCancelCommand;

    public InputPasswordWindowLoadedCommand InputPasswordWindowLoadedCommand
        => _inputPasswordWindowLoadedCommand;

    public InputPasswordWindowConfirmCommand InputPasswordWindowConfirmCommand
        => _inputPasswordWindowConfirmCommand;

    public InputPasswordWindowCancelCommand InputPasswordWindowCancelCommand
        => _inputPasswordWindowCancelCommand;

    private string _pfxFilePath = string.Empty;
    private SecureString _password = new SecureString();
    private X509CertPair? _validatedCertPair = null;

    public string PfxFilePath
    {
        get => _pfxFilePath;
        set => SetProperty(ref _pfxFilePath, value);
    }

    public SecureString Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public X509CertPair? ValidatedCertPair
    {
        get => _validatedCertPair;
        set => SetProperty(ref _validatedCertPair, value);
    }
}

using System;
using System.Collections.Generic;
using TableCloth.Commands.CertSelectWindow;
using TableCloth.Events;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class CertSelectWindowViewModelForDesigner : CertSelectWindowViewModel { }

public class CertSelectWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected CertSelectWindowViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public CertSelectWindowViewModel(
        CertSelectWindowScanCertPairCommand certSelectWindowScanCertPairCommand,
        CertSelectWindowLoadedCommand certSelectWindowLoadedCommand,
        CertSelectWindowManualCertLoadCommand certSelectWindowManualCertLoadCommand)
    {
        _certSelectWindowScanCertPairCommand = certSelectWindowScanCertPairCommand;
        _certSelectWindowLoadedCommand = certSelectWindowLoadedCommand;
        _certSelectManualCertLoadCommand = certSelectWindowManualCertLoadCommand;
    }

    private readonly CertSelectWindowScanCertPairCommand _certSelectWindowScanCertPairCommand;
    private readonly CertSelectWindowLoadedCommand _certSelectWindowLoadedCommand;
    private readonly CertSelectWindowManualCertLoadCommand _certSelectManualCertLoadCommand;

    private List<X509CertPair> _certPairs = new List<X509CertPair>();
    private X509CertPair? _selectedCertPair;

    public event EventHandler<DialogRequestEventArgs>? CloseRequested;

    public void RequestClose(object sender, DialogRequestEventArgs e)
        => this.CloseRequested?.Invoke(sender, e);


    public CertSelectWindowScanCertPairCommand CertSelectWindowScanCertPairCommand
        => _certSelectWindowScanCertPairCommand;

    public CertSelectWindowLoadedCommand CertSelectWindowLoadedCommand
        => _certSelectWindowLoadedCommand;

    public CertSelectWindowManualCertLoadCommand CertSelectWindowManualCertLoadCommand
        => _certSelectManualCertLoadCommand;

    public List<X509CertPair> CertPairs
    {
        get => _certPairs;
        set => SetProperty(ref _certPairs, value);
    }

    public X509CertPair? SelectedCertPair
    {
        get => _selectedCertPair;
        set => SetProperty(ref _selectedCertPair, value);
    }
}

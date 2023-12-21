using System;
using System.Collections.Generic;
using TableCloth.Commands;
using TableCloth.Contracts;
using TableCloth.Events;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

public class CertSelectWindowViewModel : ViewModelBase
{
    [Obsolete("This constructor should be used only in design time context.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public CertSelectWindowViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public CertSelectWindowViewModel(
        ScanCertPairCommand scanCertPairCommand,
        CertSelectWindowLoadedCommand certSelectWindowLoadedCommand,
        ManualCertLoadCommand manualCertLoadCommand)
    {
        _scanCertPairCommand = scanCertPairCommand;
        _certSelectWindowLoadedCommand = certSelectWindowLoadedCommand;
        _manualCertLoadCommand = manualCertLoadCommand;
    }

    private readonly ScanCertPairCommand _scanCertPairCommand;
    private readonly CertSelectWindowLoadedCommand _certSelectWindowLoadedCommand;
    private readonly ManualCertLoadCommand _manualCertLoadCommand;

    private List<X509CertPair> _certPairs = new List<X509CertPair>();
    private X509CertPair? _selectedCertPair;

    public event EventHandler<DialogRequestEventArgs>? OnRequestClose;

    public ScanCertPairCommand ScanCertPairCommand
        => _scanCertPairCommand;

    public CertSelectWindowLoadedCommand CertSelectWindowLoadedCommand
        => _certSelectWindowLoadedCommand;

    public ManualCertLoadCommand ManualCertLoadCommand
        => _manualCertLoadCommand;

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

    public void RequestClose(object sender, DialogRequestEventArgs e)
        => this.OnRequestClose?.Invoke(sender, e);
}

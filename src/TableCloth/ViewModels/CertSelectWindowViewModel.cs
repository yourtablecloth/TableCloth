﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Commands.CertSelectWindow;
using TableCloth.Events;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class CertSelectWindowViewModelForDesigner : CertSelectWindowViewModel
{
    public IList<X509CertPair> CertPairsForDesigner
        => DesignTimeResources.DesignTimeCertPairs;
}

public class CertSelectWindowViewModel : ViewModelBase
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected CertSelectWindowViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public CertSelectWindowViewModel(
        CertSelectWindowScanCertPairCommand certSelectWindowScanCertPairCommand,
        CertSelectWindowLoadedCommand certSelectWindowLoadedCommand,
        CertSelectWindowManualCertLoadCommand certSelectWindowManualCertLoadCommand,
        CertSelectWindowRequestConfirmCommand certSelectWindowRequestConfirmCommand,
        CertSelectWindowRequestCancelCommand certSelectWindowRequestCancelCommand)
    {
        _certSelectWindowScanCertPairCommand = certSelectWindowScanCertPairCommand;
        _certSelectWindowLoadedCommand = certSelectWindowLoadedCommand;
        _certSelectManualCertLoadCommand = certSelectWindowManualCertLoadCommand;
        _certSelectWindowRequestConfirmCommand = certSelectWindowRequestConfirmCommand;
        _certSelectWindowRequestCancelCommand = certSelectWindowRequestCancelCommand;
    }

    private readonly CertSelectWindowScanCertPairCommand _certSelectWindowScanCertPairCommand;
    private readonly CertSelectWindowLoadedCommand _certSelectWindowLoadedCommand;
    private readonly CertSelectWindowManualCertLoadCommand _certSelectManualCertLoadCommand;
    private readonly CertSelectWindowRequestConfirmCommand _certSelectWindowRequestConfirmCommand;
    private readonly CertSelectWindowRequestCancelCommand _certSelectWindowRequestCancelCommand;

#pragma warning disable IDE0028 // Simplify collection initialization
#pragma warning disable IDE0090 // Use 'new(...)'
    private List<X509CertPair> _certPairs = new List<X509CertPair>();
#pragma warning restore IDE0090 // Use 'new(...)'
#pragma warning restore IDE0028 // Simplify collection initialization
    private X509CertPair? _selectedCertPair;

    public event EventHandler<DialogRequestEventArgs>? CloseRequested;

    public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public CertSelectWindowScanCertPairCommand CertSelectWindowScanCertPairCommand
        => _certSelectWindowScanCertPairCommand;

    public CertSelectWindowLoadedCommand CertSelectWindowLoadedCommand
        => _certSelectWindowLoadedCommand;

    public CertSelectWindowManualCertLoadCommand CertSelectWindowManualCertLoadCommand
        => _certSelectManualCertLoadCommand;

    public CertSelectWindowRequestConfirmCommand CertSelectWindowRequestConfirmCommand
        => _certSelectWindowRequestConfirmCommand;

    public CertSelectWindowRequestCancelCommand CertSelectWindowRequestCancelCommand
        => _certSelectWindowRequestCancelCommand;

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

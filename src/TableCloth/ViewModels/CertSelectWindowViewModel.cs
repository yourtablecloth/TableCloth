using System;
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
    protected CertSelectWindowViewModel() { }

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

    private readonly CertSelectWindowScanCertPairCommand _certSelectWindowScanCertPairCommand = default!;
    private readonly CertSelectWindowLoadedCommand _certSelectWindowLoadedCommand = default!;
    private readonly CertSelectWindowManualCertLoadCommand _certSelectManualCertLoadCommand = default!;
    private readonly CertSelectWindowRequestConfirmCommand _certSelectWindowRequestConfirmCommand = default!;
    private readonly CertSelectWindowRequestCancelCommand _certSelectWindowRequestCancelCommand = default!;

    private List<X509CertPair> _certPairs = new List<X509CertPair>();
    private X509CertPair? _selectedCertPair;
    private string? _previousCertPairHash;

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

    public string? PreviousCertPairHash
    {
        get => _previousCertPairHash;
        set => SetProperty(ref _previousCertPairHash, value);
    }
}

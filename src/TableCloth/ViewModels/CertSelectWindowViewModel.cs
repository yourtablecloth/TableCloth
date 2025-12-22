using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
public partial class CertSelectWindowViewModelForDesigner : CertSelectWindowViewModel
{
    public IList<X509CertPair> CertPairsForDesigner
        => DesignTimeResources.DesignTimeCertPairs;
}

public partial class CertSelectWindowViewModel : ViewModelBase
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

    [RelayCommand]
    private void CertSelectWindowScanCertPair()
    {
        _certSelectWindowScanCertPairCommand.Execute(this);
    }

    private CertSelectWindowScanCertPairCommand _certSelectWindowScanCertPairCommand = default!;

    [RelayCommand]
    private void CertSelectWindowLoaded()
    {
        _certSelectWindowLoadedCommand.Execute(this);
    }

    private CertSelectWindowLoadedCommand _certSelectWindowLoadedCommand = default!;

    [RelayCommand]
    private void CertSelectManualCertLoad()
    {
        _certSelectManualCertLoadCommand.Execute(this);
    }

    private CertSelectWindowManualCertLoadCommand _certSelectManualCertLoadCommand = default!;

    [RelayCommand]
    private void CertSelectWindowRequestConfirm()
    {
        _certSelectWindowRequestConfirmCommand.Execute(this);
    }

    private CertSelectWindowRequestConfirmCommand _certSelectWindowRequestConfirmCommand = default!;

    [RelayCommand]
    private void CertSelectWindowRequestCancel()
    {
        _certSelectWindowRequestCancelCommand.Execute(this);
    }

    private CertSelectWindowRequestCancelCommand _certSelectWindowRequestCancelCommand = default!;

    [ObservableProperty]
    private List<X509CertPair> _certPairs = new List<X509CertPair>();

    [ObservableProperty]
    private X509CertPair? _selectedCertPair;

    [ObservableProperty]
    private string? _previousCertPairHash;

    public event EventHandler<DialogRequestEventArgs>? CloseRequested;

    public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);
}

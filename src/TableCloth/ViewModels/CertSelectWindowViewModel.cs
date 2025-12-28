using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
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

public partial class CertSelectWindowViewModel : ObservableObject
{
    protected CertSelectWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
    public CertSelectWindowViewModel(
        IX509CertPairScanner certPairScanner,
        IAppUserInterface appUserInterface,
        TaskFactory taskFactory)
    {
        _certPairScanner = certPairScanner;
        _appUserInterface = appUserInterface;
        _taskFactory = taskFactory;
    }

    [RelayCommand]
    private void CertSelectWindowLoaded()
    {
        CertSelectWindowScanCertPair();
    }

    [RelayCommand]
    private async Task CertSelectWindowRequestCancel()
    {
        await RequestCloseAsync(this, new DialogRequestEventArgs(false));
    }

    [RelayCommand]
    private void CertSelectWindowScanCertPair()
    {
        SelectedCertPair = default;
        CertPairs = X509CertPair.SortX509CertPairs(_certPairScanner.ScanX509Pairs(
            _certPairScanner.GetCandidateDirectories()))
            .ToList();

        if (CertPairs.Count == 1)
            SelectedCertPair = CertPairs.Single();

        if (!string.IsNullOrWhiteSpace(PreviousCertPairHash))
        {
            SelectedCertPair = CertPairs
                .Where(x => string.Equals(PreviousCertPairHash, x.CertHash, StringComparison.Ordinal))
                .FirstOrDefault();
        }
    }

    [ObservableProperty]
    private List<X509CertPair> _certPairs = new List<X509CertPair>();

    [ObservableProperty]
    private X509CertPair? _selectedCertPair;

    [ObservableProperty]
    private string? _previousCertPairHash;

    public event EventHandler<DialogRequestEventArgs>? CloseRequested;

    public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    private readonly IX509CertPairScanner _certPairScanner = default!;
    private readonly IAppUserInterface _appUserInterface = default!;
    private readonly TaskFactory _taskFactory = default!;

    private async Task LoadCertPairAsync(string? firstFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(firstFilePath) || !File.Exists(firstFilePath))
            return;

        var basePath = Path.GetDirectoryName(firstFilePath)
            .EnsureNotNull($"Cannot obtain the directory name of '{firstFilePath}'.");

        ArgumentNullException.ThrowIfNullOrWhiteSpace(basePath);
        var signCertDerPath = Path.Combine(basePath, "signCert.der");
        var signPriKeyPath = Path.Combine(basePath, "signPri.key");

        if (!File.Exists(signCertDerPath) && !File.Exists(signPriKeyPath))
            return;

        SelectedCertPair = _certPairScanner.CreateX509CertPair(signCertDerPath, signPriKeyPath);
        await RequestCloseAsync(this, new DialogRequestEventArgs(SelectedCertPair != null), cancellationToken).ConfigureAwait(false);
    }

    private async Task LoadPfxCertAsync(string? pfxFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pfxFilePath) || !File.Exists(pfxFilePath))
            return;

        var inputWindow = _appUserInterface.CreateInputPasswordWindow();
        var inputWindowViewModel = inputWindow.ViewModel;
        inputWindowViewModel.PfxFilePath = pfxFilePath;

        var inputPwdResult = inputWindow.ShowDialog();

        if (!inputPwdResult.HasValue || !inputPwdResult.Value || inputWindowViewModel.ValidatedCertPair == null)
            return;

        SelectedCertPair = inputWindowViewModel.ValidatedCertPair;
        await RequestCloseAsync(this, new DialogRequestEventArgs(SelectedCertPair != null), cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task CertSelectWindowManualCertLoad()
    {
        var ofd = new OpenFileDialog()
        {
            AddExtension = true,
            CheckFileExists = true,
            CheckPathExists = true,
            DereferenceLinks = true,
            Filter = UIStringResources.CertSelectWindow_FileOpenDialog_FilterText,
            FilterIndex = 0,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Multiselect = true,
            ReadOnlyChecked = true,
            ShowReadOnly = false,
            Title = UIStringResources.CertSelectWindow_FileOpenDialog_Text,
            ValidateNames = true,
        };

        var localLowPath = NativeMethods.GetKnownFolderPath(NativeMethods.LocalLowFolderGuid);

        if (localLowPath == null)
            throw new Exception("Cannot obtain the LocalLow folder path.");

        var npkiPath = Path.Combine(localLowPath, "NPKI");
        var userDirectories = new List<string>();

        if (Directory.Exists(npkiPath))
            userDirectories.AddRange(Directory.GetDirectories(npkiPath, "USER", SearchOption.AllDirectories));

        var removableDrives = DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Removable).Select(x => x.RootDirectory.FullName);

        ofd.CustomPlaces = new string[] { npkiPath, }
            .Concat(userDirectories)
            .Concat(removableDrives)
            .Where(x => Directory.Exists(x))
            .Select(x => new FileDialogCustomPlace(x))
            .ToList();

        var response = ofd.ShowDialog();

        if (response.HasValue && response.Value)
        {
            if (ofd.FilterIndex == 1)
                await LoadCertPairAsync(ofd.FileNames.FirstOrDefault());
            else if (ofd.FilterIndex == 2)
                await LoadPfxCertAsync(ofd.FileNames.FirstOrDefault());
        }
    }

    [RelayCommand]
    private async Task CertSelectWindowRequestConfirm()
    {
        if (SelectedCertPair != null)
            await RequestCloseAsync(this, new DialogRequestEventArgs(SelectedCertPair != null));
    }
}

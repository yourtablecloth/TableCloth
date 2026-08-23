using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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

        var inputPwdResult = _appUserInterface.ShowDialog(inputWindow);

        if (!inputPwdResult.HasValue || !inputPwdResult.Value || inputWindowViewModel.ValidatedCertPair == null)
            return;

        SelectedCertPair = inputWindowViewModel.ValidatedCertPair;
        await RequestCloseAsync(this, new DialogRequestEventArgs(SelectedCertPair != null), cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task CertSelectWindowManualCertLoad()
    {
        // 이슈 #296: WPF Microsoft.Win32.OpenFileDialog → Avalonia StorageProvider. WPF CustomPlaces(NPKI/USB
        // 즐겨찾기)와 FilterIndex 기반 분기는 폐기하고, 선택 파일 확장자(.pfx/.p12 여부)로 로드 경로를 판정한다.
        var window = ActiveOrMainWindow();
        if (window?.StorageProvider is not { } storageProvider)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UIStringResources.CertSelectWindow_FileOpenDialog_Text,
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("NPKI Certificate (*.der; *.key)") { Patterns = ["*.der", "*.key"] },
                new FilePickerFileType("PFX/P12 (*.pfx; *.p12)") { Patterns = ["*.pfx", "*.p12"] },
                FilePickerFileTypes.All,
            ],
        });

        var firstPath = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(firstPath))
            return;

        if (firstPath.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) ||
            firstPath.EndsWith(".p12", StringComparison.OrdinalIgnoreCase))
            await LoadPfxCertAsync(firstPath);
        else
            await LoadCertPairAsync(firstPath);
    }

    private static Avalonia.Controls.Window? ActiveOrMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;
        return desktop.Windows.FirstOrDefault(x => x.IsActive) ?? desktop.MainWindow;
    }

    [RelayCommand]
    private async Task CertSelectWindowRequestConfirm()
    {
        if (SelectedCertPair != null)
            await RequestCloseAsync(this, new DialogRequestEventArgs(SelectedCertPair != null));
    }
}

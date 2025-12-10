using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowManualCertLoadCommand(
    IAppUserInterface appUserInterface,
    IX509CertPairScanner certPairScanner) : ViewModelCommandBase<CertSelectWindowViewModel>, IAsyncCommand<CertSelectWindowViewModel>
{
    public override void Execute(CertSelectWindowViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(CertSelectWindowViewModel viewModel)
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
                await LoadCertPairAsync(viewModel, ofd.FileNames.FirstOrDefault());
            else if (ofd.FilterIndex == 2)
                await LoadPfxCertAsync(viewModel, ofd.FileNames.FirstOrDefault());
        }
    }

    private async Task LoadCertPairAsync(CertSelectWindowViewModel viewModel, string? firstFilePath, CancellationToken cancellationToken = default)
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

        viewModel.SelectedCertPair = certPairScanner.CreateX509CertPair(signCertDerPath, signPriKeyPath);
        await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(viewModel.SelectedCertPair != null), cancellationToken).ConfigureAwait(false);
    }

    private async Task LoadPfxCertAsync(CertSelectWindowViewModel viewModel, string? pfxFilePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pfxFilePath) || !File.Exists(pfxFilePath))
            return;

        var inputWindow = appUserInterface.CreateInputPasswordWindow();
        var inputWindowViewModel = inputWindow.ViewModel;
        inputWindowViewModel.PfxFilePath = pfxFilePath;

        var inputPwdResult = inputWindow.ShowDialog();

        if (!inputPwdResult.HasValue || !inputPwdResult.Value || inputWindowViewModel.ValidatedCertPair == null)
            return;

        viewModel.SelectedCertPair = inputWindowViewModel.ValidatedCertPair;
        await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(viewModel.SelectedCertPair != null), cancellationToken).ConfigureAwait(false);
    }
}

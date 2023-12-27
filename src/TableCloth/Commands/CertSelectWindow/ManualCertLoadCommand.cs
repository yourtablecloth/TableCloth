using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class ManualCertLoadCommand : CommandBase
{
    public ManualCertLoadCommand(
        AppUserInterface appUserInterface,
        X509CertPairScanner certPairScanner)
    {
        _appUserInterface = appUserInterface;
        _certPairScanner = certPairScanner;
    }

    private readonly AppUserInterface _appUserInterface;
    private readonly X509CertPairScanner _certPairScanner;

    public override void Execute(object? parameter)
    {
        var viewModel = parameter as CertSelectWindowViewModel;

        if (viewModel == null)
            throw new ArgumentException(nameof(parameter));

        var ofd = new OpenFileDialog()
        {
            AddExtension = true,
            CheckFileExists = true,
            CheckPathExists = true,
            DereferenceLinks = true,
            Filter = (string)Application.Current.Resources["CertSelectWindow_FileOpenDialog_FilterText"],
            FilterIndex = 0,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Multiselect = true,
            ReadOnlyChecked = true,
            ShowReadOnly = false,
            Title = (string)Application.Current.Resources["CertSelectWindow_FileOpenDialog_Text"],
            ValidateNames = true,
        };

        var npkiPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NPKI");
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
                LoadCertPair(viewModel, ofd.FileNames.FirstOrDefault());
            else if (ofd.FilterIndex == 2)
                LoadPfxCert(viewModel, ofd.FileNames.FirstOrDefault());
        }
    }

    private void LoadCertPair(CertSelectWindowViewModel viewModel, string? firstFilePath)
    {
        if (string.IsNullOrWhiteSpace(firstFilePath) || !File.Exists(firstFilePath))
            return;

        var basePath = Path.GetDirectoryName(firstFilePath)
            ?? throw new Exception($"Cannot obtain the directory name of '{firstFilePath}'.");
        var signCertDerPath = Path.Combine(basePath, "signCert.der");
        var signPriKeyPath = Path.Combine(basePath, "signPri.key");

        if (!File.Exists(signCertDerPath) && !File.Exists(signPriKeyPath))
            return;

        viewModel.SelectedCertPair = _certPairScanner.CreateX509CertPair(signCertDerPath, signPriKeyPath);
        viewModel.RequestClose(this, new DialogRequestEventArgs(viewModel.SelectedCertPair != null));
    }

    private void LoadPfxCert(CertSelectWindowViewModel viewModel, string? pfxFilePath)
    {
        if (string.IsNullOrWhiteSpace(pfxFilePath) || !File.Exists(pfxFilePath))
            return;

        var inputWindow = _appUserInterface.CreateInputPasswordWindow();
        var inputWindowViewModel = inputWindow.ViewModel;
        inputWindowViewModel.PfxFilePath = pfxFilePath;

        var inputPwdResult = inputWindow.ShowDialog();

        if (!inputPwdResult.HasValue || !inputPwdResult.Value || inputWindowViewModel.ValidatedCertPair == null)
            return;

        viewModel.SelectedCertPair = inputWindowViewModel.ValidatedCertPair;
        viewModel.RequestClose(this, new DialogRequestEventArgs(viewModel.SelectedCertPair != null));
    }
}

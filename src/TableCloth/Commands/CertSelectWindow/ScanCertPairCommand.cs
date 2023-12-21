using System;
using System.Linq;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class ScanCertPairCommand : CommandBase
{
    public ScanCertPairCommand(
        X509CertPairScanner certPairScanner)
    {
        _certPairScanner = certPairScanner;
    }

    private readonly X509CertPairScanner _certPairScanner;

    public override void Execute(object? parameter)
    {
        var viewModel = parameter as CertSelectWindowViewModel;

        if (viewModel == null)
            throw new ArgumentException(nameof(parameter));

        viewModel.SelectedCertPair = default;
        viewModel.CertPairs = _certPairScanner.ScanX509Pairs(
            _certPairScanner.GetCandidateDirectories()).ToList();

        if (viewModel.CertPairs.Count == 1)
            viewModel.SelectedCertPair = viewModel.CertPairs.Single();

    }
}

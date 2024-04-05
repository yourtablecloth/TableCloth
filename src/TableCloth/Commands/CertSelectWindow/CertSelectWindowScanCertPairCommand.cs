using System;
using System.Linq;
using TableCloth.Components;
using TableCloth.Models.Configuration;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowScanCertPairCommand(
    IX509CertPairScanner certPairScanner) : ViewModelCommandBase<CertSelectWindowViewModel>
{
    public override void Execute(CertSelectWindowViewModel viewModel)
    {
        viewModel.SelectedCertPair = default;
        viewModel.CertPairs = X509CertPair.SortX509CertPairs(certPairScanner.ScanX509Pairs(
            certPairScanner.GetCandidateDirectories()))
            .ToList();

        if (viewModel.CertPairs.Count == 1)
            viewModel.SelectedCertPair = viewModel.CertPairs.Single();

        if (!string.IsNullOrWhiteSpace(viewModel.PreviousCertPairHash))
        {
            viewModel.SelectedCertPair = viewModel.CertPairs
                .Where(x => string.Equals(viewModel.PreviousCertPairHash, x.CertHash, StringComparison.Ordinal))
                .FirstOrDefault();
        }
    }
}

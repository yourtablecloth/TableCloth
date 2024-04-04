using System.Linq;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowScanCertPairCommand(
    IX509CertPairScanner certPairScanner) : ViewModelCommandBase<CertSelectWindowViewModel>
{
    public override void Execute(CertSelectWindowViewModel viewModel)
    {
        viewModel.SelectedCertPair = default;
        viewModel.CertPairs = certPairScanner.ScanX509Pairs(
            certPairScanner.GetCandidateDirectories())
            .OrderByDescending(x => x.IsValid)
            .ToList();

        if (viewModel.CertPairs.Count == 1)
            viewModel.SelectedCertPair = viewModel.CertPairs.Single();
    }
}

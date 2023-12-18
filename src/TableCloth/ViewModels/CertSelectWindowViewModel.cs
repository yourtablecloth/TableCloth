using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TableCloth.Components;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels
{
    public class CertSelectWindowViewModel : ViewModelBase
    {
        public CertSelectWindowViewModel(
            X509CertPairScanner certPairScanner)
        {
            _certPairScanner = certPairScanner;
            _certPairs = _certPairScanner.ScanX509Pairs(_certPairScanner.GetCandidateDirectories()).ToList();
        }

        private readonly X509CertPairScanner _certPairScanner;

        private List<X509CertPair> _certPairs;
        private X509CertPair _selectedCertPair;

        public X509CertPairScanner CertPairScanner
            => _certPairScanner;

        public List<X509CertPair> CertPairs
        {
            get => _certPairs;
            set => SetProperty(ref _certPairs, value);
        }

        public X509CertPair SelectedCertPair
        {
            get => _selectedCertPair;
            set => SetProperty(ref _selectedCertPair, value);
        }

        public void RefreshCertPairs()
        {
            SelectedCertPair = null;
            CertPairs = _certPairScanner.ScanX509Pairs(_certPairScanner.GetCandidateDirectories()).ToList();

            if (CertPairs.Count == 1)
                SelectedCertPair = CertPairs.Single();
        }
    }
}

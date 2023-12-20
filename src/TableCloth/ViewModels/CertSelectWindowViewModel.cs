using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.Components;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels
{
    public class CertSelectWindowViewModel : ViewModelBase
    {
        public CertSelectWindowViewModel()
        {
        }

        private List<X509CertPair> _certPairs = new List<X509CertPair>();
        private X509CertPair? _selectedCertPair;

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
    }
}

using TableCloth.Models.Configuration;

namespace TableCloth.Contracts;

public interface ICertPairSelect
{
    X509CertPair? SelectedCertFile { get; set; }
}

using TableCloth.Models.Configuration;

namespace TableCloth.Contracts
{
    public interface ICertSelect
    {
        X509CertPair? SelectedCertFile { get; set; }
    }
}

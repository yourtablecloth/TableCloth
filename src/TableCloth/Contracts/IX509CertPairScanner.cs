using System.Collections.Generic;
using TableCloth.Models.Configuration;

namespace TableCloth.Contracts
{
    public interface IX509CertPairScanner
    {
        IEnumerable<X509CertPair> ScanX509Pairs(IEnumerable<string> rootPathList);

        IEnumerable<string> GetCandidateDirectories();

        X509CertPair CreateX509CertPair(string derFilePath, string keyFilePath);
    }
}

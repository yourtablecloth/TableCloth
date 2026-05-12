using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security;
using TableCloth.Models.Configuration;

namespace TableCloth.Components;

public interface IX509CertPairScanner
{
    ILogger Logger { get; init; }

    X509CertPair CreateX509Cert(string pfxFilePath, SecureString password);
    X509CertPair CreateX509CertPair(string derFilePath, string keyFilePath);
    IEnumerable<string> GetCandidateDirectories();
    IEnumerable<X509CertPair> ScanX509Pairs(IEnumerable<string> rootPathList);
}
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;

namespace Spork.Components.Implementations
{
    public sealed class X509CertScanner : IX509CertScanner
    {
        public X509CertScanner(ILogger<X509CertScanner> logger)
        {
            _logger = logger;
        }

        private readonly ILogger _logger;

        public IEnumerable<X509CertPair> ScanSandboxNpkiCertificates()
        {
            var root = SandboxMountPaths.NpkiCanonicalPath;

            if (!Directory.Exists(root))
                return Enumerable.Empty<X509CertPair>();

            var results = new List<X509CertPair>();
            try
            {
                ScanRecursive(root, results);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NPKI scan failed at {root}", root);
            }

            return X509CertPair.SortX509CertPairs(results);
        }

        private void ScanRecursive(string dir, List<X509CertPair> results)
        {
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                    ScanRecursive(sub, results);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping subdirectories under {dir}", dir);
            }

            try
            {
                var der = Directory.GetFiles(dir, "signCert.der").FirstOrDefault();
                var key = Directory.GetFiles(dir, "signPri.key").FirstOrDefault();

                if (string.IsNullOrEmpty(der) || string.IsNullOrEmpty(key))
                    return;

                try
                {
                    var pair = new X509CertPair(File.ReadAllBytes(der), File.ReadAllBytes(key));
                    results.Add(pair);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not load NPKI cert pair at {dir}", dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping files under {dir}", dir);
            }
        }
    }
}

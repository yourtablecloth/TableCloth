using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TableCloth.Resources;

namespace TableCloth.Models.Configuration
{
    public sealed class X509CertPairForDesigner : X509CertPair
    {
        public X509CertPairForDesigner(
            string commonName,
            DateTime beginDate,
            DateTime endDate,
            string organizationalUnit,
            string organization,
            string countryName)
        {
            CommonName = commonName;
            NotBefore = beginDate;
            NotAfter = endDate;
            OrganizationalUnit = organizationalUnit;
            Organization = organization;
            CountryName = countryName;
        }
    }

    public class X509CertPair
    {
        public static IEnumerable<X509CertPair> SortX509CertPairs(IEnumerable<X509CertPair> certPairs)
            => certPairs.OrderByDescending(x => x.IsValid).ThenBy(x => x.NotAfter).ThenBy(x => x.NotBefore);

#pragma warning disable IDE0300 // Simplify collection initialization
        private static readonly char[] Separators = new char[] { ',', };
#pragma warning restore IDE0300 // Simplify collection initialization

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected X509CertPair() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public X509CertPair(byte[] publicKey, byte[] privateKey)
        {
            PublicKey = publicKey;
            PrivateKey = privateKey;

#pragma warning disable IDE0063 // Use simple 'using' statement
            using (var cert = new X509Certificate2(publicKey))
            {
                var issuerName = cert.Issuer;

                var subject = cert.Subject
                    .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x =>
                    {
                        var parts = x.Trim().Split('=');
                        var unitName = parts.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
                        var value = parts.ElementAtOrDefault(1)?.Trim() ?? string.Empty;
                        return new KeyValuePair<string, string>(unitName, value);
                    })
                    .ToArray();

                var organizationName = subject
                    .Where(x => string.Equals(x.Key, "o", StringComparison.InvariantCultureIgnoreCase))
                    .Select(x => x.Value)
                    .FirstOrDefault();

                var usageExtension = cert.Extensions
                    .OfType<X509KeyUsageExtension>()
                    .FirstOrDefault();

                var isPersonalCert = usageExtension != null &&
                                     usageExtension.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation) &&
                                     usageExtension.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature);

#pragma warning disable IDE0305 // Simplify collection initialization
                Subject = subject.ToArray();
#pragma warning restore IDE0305 // Simplify collection initialization

                IsPersonalCert = isPersonalCert;

                CommonName = Subject
                    .Where(x => string.Equals(x.Key, "cn", StringComparison.InvariantCultureIgnoreCase))
                    .Select(x => x.Value)
                    .FirstOrDefault();

                OrganizationalUnits = Subject
                    .Where(x => string.Equals(x.Key, "ou", StringComparison.InvariantCultureIgnoreCase))
                    .Select(x => x.Value)
                    .ToArray();

                OrganizationalUnit = string.Join(",", OrganizationalUnits);

                Organization = Subject
                    .Where(x => string.Equals(x.Key, "o", StringComparison.InvariantCultureIgnoreCase))
                    .Select(x => x.Value)
                    .FirstOrDefault();

                CountryName = Subject
                    .Where(x => string.Equals(x.Key, "c", StringComparison.InvariantCultureIgnoreCase))
                    .Select(x => x.Value)
                    .FirstOrDefault();

                NotAfter = cert.NotAfter;
                NotBefore = cert.NotBefore;

                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(cert.RawData);
                    CertHash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
                }
            }
#pragma warning restore IDE0063 // Use simple 'using' statement
        }

        public byte[] PublicKey { get; }
        public byte[] PrivateKey { get; }
        public KeyValuePair<string, string>[] Subject { get; }
        public bool IsPersonalCert { get; }

        public string
#if !NETFX
            ?
#endif
            CommonName
        { get; protected set; }
        public IEnumerable<string> OrganizationalUnits { get; }
        public string
#if !NETFX
            ?
#endif
            OrganizationalUnit
        { get; protected set; }
        public string
#if !NETFX
            ?
#endif
            Organization
        { get; protected set; }
        public string
#if !NETFX
            ?
#endif
            CountryName
        { get; protected set; }

        public DateTime NotAfter { get; protected set; }
        public DateTime NotBefore { get; protected set; }
        public string CertHash { get; protected set; }

        public bool IsValid
            => NotBefore <= DateTime.Now && DateTime.Now <= NotAfter;

        public bool IsBefore
            => DateTime.Now < NotBefore;

        public bool HasExpired
            => DateTime.Now > NotAfter;

        public bool SoonExpire
            => DateTime.Now > NotAfter.Add(StringResources.Cert_ExpireWindow);

        public string Availability
        {
            get
            {
                var now = DateTime.Now;
                var expireWindow = StringResources.Cert_ExpireWindow;

                if (now < NotBefore)
                    return StringResources.Cert_Availability_MayTooEarly(now, NotBefore);

                if (now > NotAfter)
                    return UIStringResources.Cert_Availability_Expired;
                else if (now > NotAfter.Add(expireWindow))
                    return StringResources.Cert_Availability_ExpireSoon(now, NotAfter, expireWindow);

                return UIStringResources.Cert_Availability_Available;
            }
        }

        public string SubjectNameForNpkiApp
            => string.Join(",", Subject.Select(x => $"{x.Key.ToLowerInvariant()}={x.Value}"));

        public override string ToString()
            => string.Join(",", Subject.Select(x => $"{x.Key}={x.Value}"));
    }
}

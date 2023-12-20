using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using TableCloth.Resources;

namespace TableCloth.Models.Configuration
{
    public class X509CertPair
    {
        public X509CertPair(byte[] publicKey, byte[] privateKey)
		{
			PublicKey = publicKey;
			PrivateKey = privateKey;

			using (var cert = new X509Certificate2(publicKey))
			{
				var issuerName = cert.Issuer;

				var subject = cert.Subject
					.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
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

				Subject = subject.ToArray();
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
			}
		}

        public byte[] PublicKey { get; }
        public byte[] PrivateKey { get; }
        public KeyValuePair<string, string>[] Subject { get; }
        public bool IsPersonalCert { get; }

		public string
#if !NETFX
            ?
#endif
			CommonName { get; }
		public IEnumerable<string> OrganizationalUnits { get; }
		public string
#if !NETFX
            ?
#endif
			OrganizationalUnit { get; }
		public string
#if !NETFX
            ?
#endif
			Organization { get; }
		public string
#if !NETFX
            ?
#endif
			CountryName { get; }

		public DateTime NotAfter { get; }
		public DateTime NotBefore { get; }

		public bool IsValid
			=> NotBefore <= DateTime.Now && DateTime.Now <= NotAfter;

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
                    return StringResources.Cert_Availability_Expired;
                else if (now > NotAfter.Add(expireWindow))
					return StringResources.Cert_Availability_ExpireSoon(now, NotAfter, expireWindow);

				return StringResources.Cert_Availability_Available;
			}
		}

		public string SubjectNameForNpkiApp
			=> string.Join(",", Subject.Select(x => $"{x.Key.ToLowerInvariant()}={x.Value}"));

        public override string ToString()
			=> string.Join(",", Subject.Select(x => $"{x.Key}={x.Value}"));
	}
}

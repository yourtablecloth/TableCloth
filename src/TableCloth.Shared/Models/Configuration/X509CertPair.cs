using System;
using System.Collections.Generic;
using System.Linq;

namespace TableCloth.Models.Configuration
{
    public class X509CertPair
    {
        public X509CertPair(string derFilePath, string keyFilePath,
			IEnumerable<KeyValuePair<string, string>> subject,
			bool isPersonalCert)
		{
			DerFilePath = derFilePath;
			KeyFilePath = keyFilePath;
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
		}

        public string DerFilePath { get; }
        public string KeyFilePath { get; }
        public KeyValuePair<string, string>[] Subject { get; }
        public bool IsPersonalCert { get; }

		public string CommonName { get; }
		public IEnumerable<string> OrganizationalUnits { get; }
		public string OrganizationalUnit { get; }
		public string Organization { get; }
		public string CountryName { get; }

		public string SubjectNameForNpkiApp
			=> string.Join(",", Subject.Select(x => $"{x.Key.ToLowerInvariant()}={x.Value}"));

        public override string ToString()
			=> string.Join(",", Subject.Select(x => $"{x.Key}={x.Value}"));
	}
}

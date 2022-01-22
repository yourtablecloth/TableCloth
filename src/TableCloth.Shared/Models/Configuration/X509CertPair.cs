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

			SubjectOrganization = Subject
				.Where(x => string.Equals(x.Key, "o", StringComparison.InvariantCultureIgnoreCase))
				.Select(x => x.Value)
				.FirstOrDefault();
		}

        public string DerFilePath { get; }
        public string KeyFilePath { get; }
        public KeyValuePair<string, string>[] Subject { get; }
        public bool IsPersonalCert { get; }

		public string SubjectOrganization { get; }

		public string SubjectNameForNpkiApp
			=> string.Join(",", Subject.Select(x => $"{x.Key.ToLowerInvariant()}={x.Value}"));

        public override string ToString()
			=> string.Join(",", Subject.Select(x => $"{x.Key}={x.Value}"));
	}
}

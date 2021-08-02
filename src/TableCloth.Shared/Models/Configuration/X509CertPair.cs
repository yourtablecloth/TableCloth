using System;
using System.Collections.Generic;
using System.Linq;

namespace TableCloth.Models.Configuration
{
    public class X509CertPair
    {
        public X509CertPair() { }

        public string DerFilePath { get; set; }
        public string KeyFilePath { get; set; }

        public KeyValuePair<string, string>[] Subject { get; set; }
        public bool IsPersonalCert { get; set; }

		public string SubjectOrganization
        {
			get
            {
				return Subject
					.Where(x => string.Equals(x.Key, "o", StringComparison.InvariantCultureIgnoreCase))
					.Select(x => x.Value)
					.FirstOrDefault();
			}
		}

		public string SubjectNameForNpkiApp
			=> string.Join(",", Subject.Select(x => $"{x.Key.ToLowerInvariant()}={x.Value}"));

        public override string ToString()
			=> string.Join(",", Subject.Select(x => $"{x.Key}={x.Value}"));
	}
}

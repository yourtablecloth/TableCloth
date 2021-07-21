using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using TableCloth.Resources;

namespace TableCloth.Models.Configuration
{
    public sealed partial class X509CertPair
    {
        public static IEnumerable<X509CertPair> ScanX509CertPairs()
        {
            var defaultNpkiPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NPKI");

            var directoryCandidates = new List<string> {defaultNpkiPath};

            var usbDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable)
                .Select(d => d.RootDirectory.FullName);

            directoryCandidates.AddRange(usbDrives);

            return directoryCandidates.Where(Directory.Exists).SelectMany(ScanX509CertPairs);

        }

        // https://stackoverflow.com/questions/5098011/directory-enumeratefiles-unauthorizedaccessexception
        public static ICollection<X509CertPair> ScanX509CertPairs(string rootPath)
        {
            var foundFiles = new List<X509CertPair>();

            try
            {
                foreach (var dir in Directory.EnumerateDirectories(rootPath))
                {
                    // Add files in subdirectories recursively to the list
                    foundFiles.AddRange(ScanX509CertPairs(dir));
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (PathTooLongException)
            {
            }

            try
            {
                // Add files from the current directory
                var singleDerFile = Directory.EnumerateFiles(rootPath, "*.der").FirstOrDefault();
                var singleKeyFile = Directory.EnumerateFiles(rootPath, "*.key").FirstOrDefault();

                if (File.Exists(singleDerFile) && File.Exists(singleKeyFile))
                    foundFiles.Add(CreateX509CertPair(singleDerFile, singleKeyFile));
            }
            catch (UnauthorizedAccessException)
            {
            }

            return foundFiles;
        }

        public static X509CertPair CreateX509CertPair(string derFilePath, string keyFilePath)
        {
            if (!File.Exists(derFilePath))
                throw new FileNotFoundException(StringResources.Error_Cannot_Find_CertFile, derFilePath);

            if (!File.Exists(keyFilePath))
                throw new FileNotFoundException(StringResources.Error_Cannot_Find_KeyFile, keyFilePath);

            using var cert = new X509Certificate2(derFilePath);

            var issuerName = cert.Issuer;
            var subjectNamePairs = cert.Subject
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    var parts = x.Trim().Split('=');
                    var unitName = parts.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
                    var value = parts.ElementAtOrDefault(1)?.Trim() ?? string.Empty;
                    return new KeyValuePair<string, string>(unitName, value);
                })
                .ToArray();

            var organizationName = subjectNamePairs
                .Where(x => string.Equals(x.Key, "o", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Value)
                .FirstOrDefault();

            var usageExtension = cert.Extensions
                .OfType<X509KeyUsageExtension>()
                .FirstOrDefault();

            var isPersonalCert = usageExtension != null &&
                                 usageExtension.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation) &&
                                 usageExtension.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature);

            return new X509CertPair
            {
                Subject = subjectNamePairs.ToArray(),
                IsPersonalCert = isPersonalCert,
                DerFilePath = derFilePath,
                KeyFilePath = keyFilePath,
            };
        }
    }

    public partial class X509CertPair
    {
        private X509CertPair() { }

        public string DerFilePath { get; init; }
        public string KeyFilePath { get; init; }

        public KeyValuePair<string, string>[] Subject { get; init; }
        public bool IsPersonalCert { get; init; }

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
			=> string.Join(',', Subject.Select(x => $"{x.Key.ToLowerInvariant()}={x.Value}"));

        public override string ToString()
			=> string.Join(',', Subject.Select(x => $"{x.Key}={x.Value}"));
	}
}

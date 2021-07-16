using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace TableCloth
{
    public sealed class X509CertPair
    {
		public static IEnumerable<X509CertPair> ScanX509CertPairs()
		{
			var list = new List<X509CertPair>();
            var directoryCandidates = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NPKI"),
            };

            foreach (var eachDrive in DriveInfo.GetDrives())
			{
				if (eachDrive.DriveType != DriveType.Removable)
					continue;

				directoryCandidates.Add(eachDrive.RootDirectory.FullName);
			}

			foreach (var eachDirectoryCandidate in directoryCandidates)
				if (Directory.Exists(eachDirectoryCandidate))
					list.AddRange(ScanX509CertPairs(eachDirectoryCandidate));

			return list.ToArray();
		}

		// https://stackoverflow.com/questions/5098011/directory-enumeratefiles-unauthorizedaccessexception
		public static IEnumerable<X509CertPair> ScanX509CertPairs(string rootPath)
		{
			var foundFiles = Enumerable.Empty<X509CertPair>();

			try
			{
				IEnumerable<string> subDirs = Directory.EnumerateDirectories(rootPath);
				foreach (string dir in subDirs)
				{
					// Add files in subdirectories recursively to the list
					foundFiles = foundFiles.Concat(ScanX509CertPairs(dir));
				}
			}
			catch (UnauthorizedAccessException) { }
			catch (PathTooLongException) { }

			try
			{
				// Add files from the current directory
				var singleDerFile = Directory.EnumerateFiles(rootPath, "*.der").FirstOrDefault();
				var singleKeyFile = Directory.EnumerateFiles(rootPath, "*.key").FirstOrDefault();

				if (File.Exists(singleDerFile) && File.Exists(singleKeyFile))
					foundFiles = foundFiles.Concat(new X509CertPair[] { CreateX509CertPair(singleDerFile, singleKeyFile) });
			}
			catch (UnauthorizedAccessException) { }

			return foundFiles;
		}

		public static X509CertPair CreateX509CertPair(string derFilePath, string keyFilePath)
        {
			if (!File.Exists(derFilePath))
                throw new FileNotFoundException("Certification file (.der) does not exists.", derFilePath);

            if (!File.Exists(keyFilePath))
                throw new FileNotFoundException("Private key file (.key) does not exists.", keyFilePath);

            using var cert = new X509Certificate2(derFilePath);

            var issuerName = cert.Issuer;
            var subjectNamePairs = cert.Subject
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                {
                    var parts = x.Trim().Split('=', StringSplitOptions.None);
                    var unitName = parts.ElementAtOrDefault(0)?.Trim() ?? string.Empty;
                    var value = parts.ElementAtOrDefault(1)?.Trim() ?? string.Empty;
                    return new KeyValuePair<string, string>(unitName, value);
                });
            var organizationName = subjectNamePairs
                .Where(x => string.Equals(x.Key, "o", StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Value)
                .FirstOrDefault();
            var usageExtension = cert.Extensions
                .Cast<X509Extension>()
                .Where(x => x is X509KeyUsageExtension)
                .Select(x => x as X509KeyUsageExtension)
                .FirstOrDefault();
            var isPersonalCert = usageExtension != null &&
                usageExtension.KeyUsages.HasFlag(X509KeyUsageFlags.NonRepudiation) &&
                usageExtension.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature);

            return new X509CertPair()
            {
                Subject = subjectNamePairs.ToArray(),
                IsPersonalCert = isPersonalCert,
                DerFilePath = derFilePath,
                KeyFilePath = keyFilePath,
            };
        }

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

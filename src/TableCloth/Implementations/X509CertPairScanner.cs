using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using TableCloth.Contracts;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class X509CertPairScanner : IX509CertPairScanner
    {
        public IEnumerable<string> GetCandidateDirectories()
        {
            var defaultNpkiPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NPKI");

            var directoryCandidates = new List<string>();

            if (Directory.Exists(defaultNpkiPath))
                directoryCandidates.Add(defaultNpkiPath);

            var usbDrives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable)
                .Where(d => Directory.Exists(d.RootDirectory.FullName))
                .Select(d => d.RootDirectory.FullName);

            directoryCandidates.AddRange(usbDrives);
            return directoryCandidates;
        }

        // https://stackoverflow.com/questions/5098011/directory-enumeratefiles-unauthorizedaccessexception
        public IEnumerable<X509CertPair> ScanX509Pairs(IEnumerable<string> rootPathList)
        {
            var foundFiles = new List<X509CertPair>();

            foreach (var eachRootPath in rootPathList)
            {
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(eachRootPath))
                    {
                        // Add files in subdirectories recursively to the list
                        foundFiles.AddRange(ScanX509Pairs(new string[] { dir }));
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
                    var singleDerFile = Directory.EnumerateFiles(eachRootPath, "*.der").FirstOrDefault();
                    var singleKeyFile = Directory.EnumerateFiles(eachRootPath, "*.key").FirstOrDefault();

                    if (File.Exists(singleDerFile) && File.Exists(singleKeyFile))
                        foundFiles.Add(CreateX509CertPair(singleDerFile, singleKeyFile));
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return foundFiles;
        }

        public X509CertPair CreateX509CertPair(string derFilePath, string keyFilePath)
        {
            if (!File.Exists(derFilePath))
                throw new FileNotFoundException(StringResources.Error_Cannot_Find_CertFile, derFilePath);

            if (!File.Exists(keyFilePath))
                throw new FileNotFoundException(StringResources.Error_Cannot_Find_KeyFile, keyFilePath);

            using (var cert = new X509Certificate2(derFilePath))
            {
                var issuerName = cert.Issuer;
                var subjectNamePairs = cert.Subject
                    .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
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

                return new X509CertPair()
                {
                    Subject = subjectNamePairs.ToArray(),
                    IsPersonalCert = isPersonalCert,
                    DerFilePath = derFilePath,
                    KeyFilePath = keyFilePath,
                };
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using PnPeople.Security;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class X509CertPairScanner(
    ILogger<X509CertPairScanner> logger) : IX509CertPairScanner
{
    public ILogger Logger { get; init; } = logger;

    public IEnumerable<string> GetCandidateDirectories()
    {
        var localLowPath = NativeMethods.GetKnownFolderPath(NativeMethods.LocalLowFolderGuid);

        if (localLowPath == null)
            throw new Exception("Cannot obtain the LocalLow folder path.");

        var defaultNpkiPath = Path.Combine(localLowPath, "NPKI");
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
            catch (Exception e)
            {
                Logger.LogWarning(e, "{message}", StringResources.TableCloth_Log_DirectoryEnumFail_ProhibitTranslation(eachRootPath, e));
            }

            try
            {
                // Add files from the current directory
                var singleDerFile = Directory.GetFiles(eachRootPath, "signCert.der").FirstOrDefault();
                var singleKeyFile = Directory.GetFiles(eachRootPath, "signPri.key").FirstOrDefault();

                if (File.Exists(singleDerFile) && File.Exists(singleKeyFile))
                    foundFiles.Add(CreateX509CertPair(singleDerFile, singleKeyFile));
            }
            catch (UnauthorizedAccessException uae)
            {
                Logger.LogWarning(uae, "Cannot load X509 cert pair - {eachRootPath}", eachRootPath);
            }
            catch (PathTooLongException ptle)
            {
                Logger.LogWarning(ptle, "Cannot load X509 cert pair - {eachRootPath}", eachRootPath);
            }
            catch (AggregateException ae)
            {
                Logger.LogWarning(ae.InnerException ?? ae, "Cannot load X509 cert pair - {eachRootPath}", eachRootPath);
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "Cannot load X509 cert pair - {eachRootPath}", eachRootPath);
            }
        }

        return foundFiles;
    }

    public X509CertPair CreateX509CertPair(string derFilePath, string keyFilePath)
    {
        if (!File.Exists(derFilePath))
            TableClothAppException.Throw(ErrorStrings.Error_Cannot_Find_CertFile, derFilePath);

        if (!File.Exists(keyFilePath))
            TableClothAppException.Throw(ErrorStrings.Error_Cannot_Find_KeyFile, keyFilePath);

        return new X509CertPair(
            File.ReadAllBytes(derFilePath),
            File.ReadAllBytes(keyFilePath));
    }

    public X509CertPair CreateX509Cert(string pfxFilePath, SecureString password)
    {
        if (!File.Exists(pfxFilePath))
            TableClothAppException.Throw(ErrorStrings.Error_Cannot_Find_PfxFile, pfxFilePath);

        var copiedPassword = CertPrivateKeyHelper.CopyFromSecureString(password);

        using X509Certificate2 cert = new X509Certificate2(pfxFilePath, copiedPassword, X509KeyStorageFlags.Exportable);
        var publicKey = cert.Export(X509ContentType.Cert);

        var rsaPrivateKey = cert.GetRSAPrivateKey().EnsureNotNull("Cannot obtain RSA private key.");
        var privateKey = rsaPrivateKey.ExportEncryptedPkcs8PrivateKey(copiedPassword,
            new PbeParameters(PbeEncryptionAlgorithm.TripleDes3KeyPkcs12, HashAlgorithmName.SHA1, 2048));

        return new X509CertPair(publicKey, privateKey);
    }
}

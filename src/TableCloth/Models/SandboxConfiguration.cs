using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace TableCloth.Models
{
    [Serializable]
    public sealed class SandboxConfiguration
    {
        public X509CertPair CertPair { get; init; }
        public bool EnableMicrophone { get; init; }
        public bool EnableWebCam { get; init; }
        public bool EnablePrinters { get; init; }
        public ICollection<InternetService> SelectedServices { get; init; }

        public string AssetsDirectoryPath { get; internal set; }

        public string SerializeToXml() => SandboxConfigurationGenerator.SerializeToXml(this);
    }

    [XmlRoot("Configuration")]
    public sealed partial class SandboxConfigurationGenerator
    {
        internal const string DefaultLogonCommand = @"C:\assets\StartupScript.cmd";
        internal const string Enable = "Enable";
        internal const string Disable = "Disable";

        [XmlElement]
        public string AudioInput { get; set; }

        [XmlElement]
        public string VideoInput { get; set; }

        [XmlElement]
        public string PrinterRedirection { get; set; }

        [XmlElement]
        public string LogonCommand { get; set; } = DefaultLogonCommand;

        [XmlArray, XmlArrayItem(typeof(MappedFolder))]
        public List<MappedFolder> MappedFolders { get; } = new();

        [Serializable, XmlType]
        public sealed class MappedFolder
        {
            public const string DefaultAssetPath = @"C:\assets";

            [XmlElement]
            public string HostFolder { get; set; }

            [XmlElement]
            public string SandboxFolder { get; set; }

            [XmlElement]
            public string ReadOnly { get; set; } = bool.TrueString;
        }
    }

    public partial class SandboxConfigurationGenerator
    {
        public sealed class SandboxConfigToXmlWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        public static string SerializeToXml(SandboxConfiguration config)
        {
            var serializer = new XmlSerializer(typeof(SandboxConfigurationGenerator));
            var @namespace = new XmlSerializerNamespaces(new[] { new XmlQualifiedName(string.Empty) });

            using var contentStream = new SandboxConfigToXmlWriter();

            serializer.Serialize(contentStream, Create(config), @namespace);

            return contentStream.ToString();
        }

        public static SandboxConfigurationGenerator Create(SandboxConfiguration config)
        {
            var generator = new SandboxConfigurationGenerator
            {
                AudioInput = config.EnableMicrophone ? Enable : Disable,
                VideoInput = config.EnableWebCam ? Enable : Disable,
                PrinterRedirection = config.EnablePrinters ? Enable : Disable,
            };

            generator.SetAssetDirectory(config);

            return generator;
        }
        public void SetAssetDirectory(SandboxConfiguration config)
        {
            if (!Directory.Exists(config.AssetsDirectoryPath))
                return;

            MappedFolders.Clear();

            MappedFolders.Add(new MappedFolder
            {
                HostFolder = config.AssetsDirectoryPath,
                SandboxFolder = MappedFolder.DefaultAssetPath,
                ReadOnly = bool.TrueString,
            });

            if (config.CertPair == null)
                return;

            var certAssetsDirectoryPath = Path.Combine(config.AssetsDirectoryPath, "certs");
            if (!Directory.Exists(certAssetsDirectoryPath))
                Directory.CreateDirectory(certAssetsDirectoryPath);

            var destDerFilePath = Path.Combine(
                certAssetsDirectoryPath,
                Path.GetFileName(config.CertPair.DerFilePath));


            var destKeyFileName = Path.Combine(
                certAssetsDirectoryPath,
                Path.GetFileName(config.CertPair.KeyFilePath));

            File.Copy(config.CertPair.DerFilePath, destDerFilePath, true);
            File.Copy(config.CertPair.KeyFilePath, destKeyFileName, true);

            var candidatePath = Path.Join("AppData", "LocalLow", "NPKI", config.CertPair.SubjectOrganization);
            if (config.CertPair.IsPersonalCert)
                candidatePath = Path.Join(candidatePath, "USER", config.CertPair.SubjectNameForNpkiApp);
            candidatePath = Path.Join(@"C:\Users\WDAGUtilityAccount", candidatePath);

            MappedFolders.Add(new MappedFolder
            {
                HostFolder = certAssetsDirectoryPath,
                SandboxFolder = candidatePath,
                ReadOnly = bool.FalseString,
            });
        }
    }
}

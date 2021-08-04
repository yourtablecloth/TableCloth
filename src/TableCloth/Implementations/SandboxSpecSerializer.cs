using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Contracts;
using TableCloth.Implementations.WindowsSandbox;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class SandboxSpecSerializer : ISandboxSpecSerializer
    {
        public SandboxSpecSerializer(IAppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly IAppMessageBox _appMessageBox;

        public string SerializeSandboxSpec(SandboxConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var unavailableDirectories = configuration.MappedFolders
                .Where(x => !Directory.Exists(x.HostFolder));

            _appMessageBox.DisplayError(StringResources.Error_HostFolder_Unavailable(unavailableDirectories.Select(x => x.HostFolder)), false);
            configuration.MappedFolders.RemoveAll(x => unavailableDirectories.Contains(x));

            var serializer = new XmlSerializer(typeof(SandboxConfiguration));
            var @namespace = new XmlSerializerNamespaces(new[] { new XmlQualifiedName(string.Empty) });
            var targetEncoding = new UTF8Encoding(false);

            using var memStream = new MemoryStream();
            var contentStream = new StreamWriter(memStream);
            serializer.Serialize(contentStream, configuration, @namespace);
            return targetEncoding.GetString(memStream.ToArray());
        }
    }
}

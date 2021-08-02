using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Contracts;
using TableCloth.Implementations.WindowsSandbox;

namespace TableCloth.Implementations
{
    public sealed class SandboxSpecSerializer : ISandboxSpecSerializer
    {
        public string SerializeSandboxSpec(SandboxConfiguration configuration)
            => SerializeToXml(configuration);

        public static string SerializeToXml<T>(T objectToSerialize)
            where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            var @namespace = new XmlSerializerNamespaces(new[] { new XmlQualifiedName(string.Empty) });
            var targetEncoding = new UTF8Encoding(false);

            using var memStream = new MemoryStream();
            var contentStream = new StreamWriter(memStream);
            serializer.Serialize(contentStream, objectToSerialize, @namespace);
            return targetEncoding.GetString(memStream.ToArray());
        }
    }
}

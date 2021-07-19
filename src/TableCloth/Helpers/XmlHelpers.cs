using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace TableCloth.Helpers
{
    static class XmlHelpers
    {
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

        public static T DeserializeFromXml<T>(Stream readableStream)
            where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            var targetEncoding = new UTF8Encoding(false);

            using var contentStream = new StreamReader(readableStream, targetEncoding);
            return serializer.Deserialize(contentStream) as T;
        }
    }
}

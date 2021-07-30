using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace TableCloth.Helpers
{
    internal static class XmlHelpers
    {
        public static string SerializeToXml<T>(T objectToSerialize)
            where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            var @namespace = new XmlSerializerNamespaces(new[] { new XmlQualifiedName(string.Empty) });
            var targetEncoding = new UTF8Encoding(false);

#if NETFX
            using (var memStream = new MemoryStream())
            {
                var contentStream = new StreamWriter(memStream);
                serializer.Serialize(contentStream, objectToSerialize, @namespace);
                return targetEncoding.GetString(memStream.ToArray());
            }
#else
            using var memStream = new MemoryStream();
            var contentStream = new StreamWriter(memStream);
            serializer.Serialize(contentStream, objectToSerialize, @namespace);
            return targetEncoding.GetString(memStream.ToArray());
#endif
        }

        public static T DeserializeFromXml<T>(Stream readableStream)
            where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            var xmlReaderSetting = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
            };

#if NETFX
            using (var contentStream = XmlReader.Create(readableStream, xmlReaderSetting))
            {
                return (T)serializer.Deserialize(contentStream);
            }
#else
            using var contentStream = XmlReader.Create(readableStream, xmlReaderSetting);
            return (T)serializer.Deserialize(contentStream);
#endif
        }
    }
}

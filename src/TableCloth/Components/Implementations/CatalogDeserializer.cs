using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Models.Catalog;

namespace TableCloth.Components.Implementations;

public sealed class CatalogDeserializer : ICatalogDeserializer
{
    public CatalogDocument? Deserialize(Stream catalogStream, Encoding targetEncoding)
    {
        using var catalogStreamReader = new StreamReader(catalogStream, targetEncoding, true);
        return Deserialize(catalogStreamReader);
    }

    public CatalogDocument? Deserialize(TextReader textReader)
    {
        var serializer = new XmlSerializer(typeof(CatalogDocument));
        var xmlReaderSetting = new XmlReaderSettings()
        {
            XmlResolver = null,
            DtdProcessing = DtdProcessing.Prohibit,
        };

        using var contentStream = XmlReader.Create(textReader, xmlReaderSetting);
        var document = serializer.Deserialize(contentStream) as CatalogDocument;
        return document;
    }
}

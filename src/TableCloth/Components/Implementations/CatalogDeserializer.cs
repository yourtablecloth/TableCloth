using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
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
        var xmlReaderSettings = new XmlReaderSettings()
        {
            XmlResolver = null,
            DtdProcessing = DtdProcessing.Prohibit,
        };

        using var xmlReader = XmlReader.Create(textReader, xmlReaderSettings);
        return XmlCatalogParser.ParseCatalogDocument(xmlReader);
    }
}

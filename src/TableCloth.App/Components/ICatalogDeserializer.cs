using System.IO;
using System.Text;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public interface ICatalogDeserializer
{
    CatalogDocument? Deserialize(Stream catalogStream, Encoding targetEncoding);

    CatalogDocument? Deserialize(TextReader catalogStreamReader);
}

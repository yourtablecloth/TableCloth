using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class CatalogDeserializer : ICatalogDeserializer
    {
        public CatalogDocument DeserializeCatalog(Uri targetUri)
        {
            using var webClient = new WebClient()
            {
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore),
            };
            webClient.Headers.Add("User-Agent", StringResources.UserAgentText);
            webClient.QueryString.Add("ts", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));

            using var catalogStream = webClient.OpenRead(StringResources.CatalogUrl);
            var catalog = DeserializeFromXml<CatalogDocument>(catalogStream);
            return catalog;
        }

        private static T DeserializeFromXml<T>(Stream readableStream)
            where T : class
        {
            var serializer = new XmlSerializer(typeof(T));
            var xmlReaderSetting = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
            };

            using var contentStream = XmlReader.Create(readableStream, xmlReaderSetting);
            return (T)serializer.Deserialize(contentStream);
        }
    }
}

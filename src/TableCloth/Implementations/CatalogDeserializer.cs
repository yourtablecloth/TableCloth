using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class CatalogDeserializer : ICatalogDeserializer
    {
        private DateTimeOffset? _catalogLastModified = default;
        private DateTimeOffset? _ieModeListLastModified = default;

        public DateTimeOffset? CatalogLastModified => _catalogLastModified;

        public CatalogDocument DeserializeCatalog()
        {
            var httpClient = Shared.HttpClientFactory.Value;

            var uriBuilder = new UriBuilder(new Uri(StringResources.CatalogUrl, UriKind.Absolute));

            var queryKeyValues = HttpUtility.ParseQueryString(uriBuilder.Query);
            queryKeyValues["ts"] = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            uriBuilder.Query = queryKeyValues.ToString();

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            httpRequest.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true, NoStore = true, };
            httpRequest.Headers.UserAgent.TryParseAdd(StringResources.UserAgentText);

            var httpResponse = httpClient.Send(httpRequest);
            _catalogLastModified = httpResponse.Content.Headers.LastModified;

            using var catalogStream = httpResponse.Content.ReadAsStream();
            var serializer = new XmlSerializer(typeof(CatalogDocument));
            var xmlReaderSetting = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
            };

            using var contentStream = XmlReader.Create(catalogStream, xmlReaderSetting);
            return (CatalogDocument)serializer.Deserialize(contentStream);
        }

        public DateTimeOffset? IEModeListLastModified => _ieModeListLastModified;

        public IEModeListDocument DeserializeIEModeList()
        {
            var httpClient = Shared.HttpClientFactory.Value;

            var uriBuilder = new UriBuilder(new Uri(StringResources.IEModeListUrl, UriKind.Absolute));

            var queryKeyValues = HttpUtility.ParseQueryString(uriBuilder.Query);
            queryKeyValues["ts"] = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            uriBuilder.Query = queryKeyValues.ToString();

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            httpRequest.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true, NoStore = true, };
            httpRequest.Headers.UserAgent.TryParseAdd(StringResources.UserAgentText);

            var httpResponse = httpClient.Send(httpRequest);
            _ieModeListLastModified = httpResponse.Content.Headers.LastModified;

            using var ieModeListStream = httpResponse.Content.ReadAsStream();
            var serializer = new XmlSerializer(typeof(IEModeListDocument));
            var xmlReaderSetting = new XmlReaderSettings()
            {
                XmlResolver = null,
                DtdProcessing = DtdProcessing.Prohibit,
            };

            using var contentStream = XmlReader.Create(ieModeListStream, xmlReaderSetting);
            return (IEModeListDocument)serializer.Deserialize(contentStream);

        }
    }
}

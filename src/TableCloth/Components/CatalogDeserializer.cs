using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class CatalogDeserializer
{
    public CatalogDeserializer(
        IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private readonly IHttpClientFactory _httpClientFactory;

    private DateTimeOffset? _catalogLastModified = default;

    public DateTimeOffset? CatalogLastModified => _catalogLastModified;

    public async Task<CatalogDocument> DeserializeCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateTableClothHttpClient();
        var uriBuilder = new UriBuilder(new Uri(StringResources.CatalogUrl, UriKind.Absolute));

        var queryKeyValues = HttpUtility.ParseQueryString(uriBuilder.Query);
        queryKeyValues["ts"] = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
        uriBuilder.Query = queryKeyValues.ToString();

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
        httpRequest.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true, NoStore = true, };
        httpRequest.Headers.UserAgent.TryParseAdd(StringResources.UserAgentText);

        var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        _catalogLastModified = httpResponse.Content.Headers.LastModified;

        using var catalogStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var serializer = new XmlSerializer(typeof(CatalogDocument));
        var xmlReaderSetting = new XmlReaderSettings()
        {
            XmlResolver = null,
            DtdProcessing = DtdProcessing.Prohibit,
        };

        using var contentStream = XmlReader.Create(catalogStream, xmlReaderSetting);
        var document = serializer.Deserialize(contentStream) as CatalogDocument;

        if (document == null)
            throw new Exception(StringResources.HostessError_CatalogLoadFailure(null));

        return document;
    }
}

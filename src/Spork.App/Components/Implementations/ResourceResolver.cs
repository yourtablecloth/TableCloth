using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using TableCloth;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Spork.Components.Implementations
{
    public sealed class ResourceResolver : IResourceResolver
    {
        public ResourceResolver(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private readonly IHttpClientFactory _httpClientFactory;

        private DateTimeOffset? _catalogLastModified = default;

        public DateTimeOffset? CatalogLastModified => _catalogLastModified;

        public async Task<ApiInvokeResult<CatalogDocument>> DeserializeCatalogAsync(
            CancellationToken cancellationToken = default)
        {
            var httpClient = _httpClientFactory.CreateTableClothHttpClient();
            var uriBuilder = new UriBuilder(new Uri(ConstantStrings.CatalogUrl, UriKind.Absolute));

            var queryKeyValues = HttpUtility.ParseQueryString(uriBuilder.Query);
            queryKeyValues[ConstantStrings.QueryString_Timestamp_Key] = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            uriBuilder.Query = queryKeyValues.ToString();

            var httpRequest = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            httpRequest.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true, NoStore = true, };
            httpRequest.Headers.UserAgent.TryParseAdd(ConstantStrings.UserAgentText);
            // 사용률/버전 채택률 측정용 앱 식별 헤더(서버 로그용). client=spork 는 샌드박스가 실제로 실행됐음을 뜻해
            // host 신호(앱 실행)와 구분된다. 브라우저 위장 UA 는 그대로 두고 X- 헤더만 추가(PII 없음).
            httpRequest.Headers.TryAddWithoutValidation("X-TableCloth-Version", Helpers.GetAppVersion());
            httpRequest.Headers.TryAddWithoutValidation("X-TableCloth-Client", $"spork; {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");

            var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            _catalogLastModified = httpResponse.Content.Headers.LastModified;

            using (var catalogStream = await httpResponse.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                var xmlReaderSettings = new XmlReaderSettings()
                {
                    XmlResolver = null,
                    DtdProcessing = DtdProcessing.Prohibit,
                };

                using (var xmlReader = XmlReader.Create(catalogStream, xmlReaderSettings))
                {
                    var document = XmlCatalogParser.ParseCatalogDocument(xmlReader);

                    if (document == null)
                        TableClothAppException.Throw(StringResources.Error_With_Exception(ErrorStrings.Error_CatalogLoadFailure, null));

                    return document;
                }
            }
        }
    }
}

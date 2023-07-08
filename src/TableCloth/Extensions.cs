using System.Net.Http;

namespace TableCloth
{
    internal static class Extensions
    {
        public static HttpClient CreateTableClothHttpClient(this IHttpClientFactory httpClientFactory)
            => httpClientFactory!.CreateClient(nameof(TableCloth));
    }
}

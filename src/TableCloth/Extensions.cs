using System;
using System.Net.Http;

namespace TableCloth
{
    internal static class Extensions
    {
        public static HttpClient CreateTableClothHttpClient(this IHttpClientFactory httpClientFactory)
            => httpClientFactory!.CreateClient(nameof(TableCloth));

        public static T AssignService<T>(this IServiceProvider serviceProvider, out T target)
            where T : class
            => target = serviceProvider.GetService(typeof(T)) as T;
    }
}

using System;
using System.Net.Http;
using TableCloth.Resources;

namespace TableCloth
{
    internal static class Shared
    {
        public static readonly Lazy<HttpClient> HttpClientFactory = new(() =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", StringResources.UserAgentText);
            return client;

        }, true);
    }
}

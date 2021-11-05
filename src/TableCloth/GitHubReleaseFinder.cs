using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace TableCloth
{
    internal static class GitHubReleaseFinder
    {
        private static readonly Lazy<HttpClient> _httpClientFactory = new Lazy<HttpClient>(() =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", StringResources.UserAgentText);
            return client;

        }, true);

        internal static async Task<string> GetLatestVersion(string owner, string repoName)
        {
            var targetUri = new Uri($"https://api.github.com/repos/{owner}/{repoName}/releases/latest", UriKind.Absolute);
            var httpClient = _httpClientFactory.Value;

            using var licenseDescription = await httpClient.GetStreamAsync(targetUri);
            var jsonDocument = await JsonDocument.ParseAsync(licenseDescription).ConfigureAwait(false);
            return jsonDocument.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
        }

        internal static async Task<Uri> GetDownloadUrl(string owner, string repoName)
        {
            var targetUri = new Uri($"https://api.github.com/repos/{owner}/{repoName}/releases/latest", UriKind.Absolute);
            var httpClient = _httpClientFactory.Value;

            using var licenseDescription = await httpClient.GetStreamAsync(targetUri);
            var jsonDocument = await JsonDocument.ParseAsync(licenseDescription).ConfigureAwait(false);

            if (Uri.TryCreate(jsonDocument.RootElement.GetProperty("html_url").GetString(), UriKind.Absolute, out Uri result))
                return result;
            else
                return new Uri($"https://github.com/{owner}/{repoName}/releases", UriKind.Absolute);
        }
    }
}

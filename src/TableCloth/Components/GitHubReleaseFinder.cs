using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TableCloth.Components
{
    public sealed class GitHubReleaseFinder
    {
        public GitHubReleaseFinder(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        private readonly IHttpClientFactory _httpClientFactory;

        public async Task<string> GetLatestVersion(string owner, string repoName)
        {
            var targetUri = new Uri($"https://api.github.com/repos/{owner}/{repoName}/releases/latest", UriKind.Absolute);
            var httpClient = _httpClientFactory.CreateTableClothHttpClient();

            using var licenseDescription = await httpClient.GetStreamAsync(targetUri);
            var jsonDocument = await JsonDocument.ParseAsync(licenseDescription).ConfigureAwait(false);
            return jsonDocument.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
        }

        public async Task<Uri> GetDownloadUrl(string owner, string repoName)
        {
            var targetUri = new Uri($"https://api.github.com/repos/{owner}/{repoName}/releases/latest", UriKind.Absolute);
            var httpClient = _httpClientFactory.CreateTableClothHttpClient();

            using var licenseDescription = await httpClient.GetStreamAsync(targetUri);
            var jsonDocument = await JsonDocument.ParseAsync(licenseDescription).ConfigureAwait(false);

            if (Uri.TryCreate(jsonDocument.RootElement.GetProperty("html_url").GetString(), UriKind.Absolute, out var result))
                return result;
            else
                return new Uri($"https://github.com/{owner}/{repoName}/releases", UriKind.Absolute);
        }
    }
}

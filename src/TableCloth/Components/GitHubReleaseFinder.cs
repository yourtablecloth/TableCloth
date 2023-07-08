using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace TableCloth.Components
{
    public sealed class GitHubReleaseFinder
    {
        public async Task<string> GetLatestVersion(string owner, string repoName)
        {
            var targetUri = new Uri($"https://api.github.com/repos/{owner}/{repoName}/releases/latest", UriKind.Absolute);
            var httpClient = Shared.HttpClientFactory.Value;

            using var licenseDescription = await httpClient.GetStreamAsync(targetUri);
            var jsonDocument = await JsonDocument.ParseAsync(licenseDescription).ConfigureAwait(false);
            return jsonDocument.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
        }

        public async Task<Uri> GetDownloadUrl(string owner, string repoName)
        {
            var targetUri = new Uri($"https://api.github.com/repos/{owner}/{repoName}/releases/latest", UriKind.Absolute);
            var httpClient = Shared.HttpClientFactory.Value;

            using var licenseDescription = await httpClient.GetStreamAsync(targetUri);
            var jsonDocument = await JsonDocument.ParseAsync(licenseDescription).ConfigureAwait(false);

            if (Uri.TryCreate(jsonDocument.RootElement.GetProperty("html_url").GetString(), UriKind.Absolute, out var result))
                return result;
            else
                return new Uri($"https://github.com/{owner}/{repoName}/releases", UriKind.Absolute);
        }
    }
}

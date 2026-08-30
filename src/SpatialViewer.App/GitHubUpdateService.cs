using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SpatialViewer.Product;

internal sealed record GitHubReleaseInfo(string TagName, string HtmlUrl, string? Name, string? Body, DateTimeOffset? PublishedAt)
{
    public string DisplayVersion => TagName.TrimStart('v', 'V');
}

internal static class GitHubUpdateService
{
    private static readonly HttpClient Client = CreateClient();

    public static async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string repository, CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync($"https://api.github.com/repos/{repository}/releases/latest", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = json.RootElement;
        return new GitHubReleaseInfo(
            root.GetProperty("tag_name").GetString() ?? string.Empty,
            root.GetProperty("html_url").GetString() ?? $"https://github.com/{repository}/releases",
            root.TryGetProperty("name", out var name) ? name.GetString() : null,
            root.TryGetProperty("body", out var body) ? body.GetString() : null,
            root.TryGetProperty("published_at", out var published) && published.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(published.GetString(), out var timestamp) ? timestamp : null);
    }

    public static bool IsNewer(string availableTag, Version current)
    {
        var normalized = availableTag.Trim().TrimStart('v', 'V');
        var separator = normalized.IndexOfAny(['-', '+']);
        if (separator >= 0) normalized = normalized[..separator];
        return Version.TryParse(normalized, out var available) && available > current;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SpatialViewer", "0.2.1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }
}

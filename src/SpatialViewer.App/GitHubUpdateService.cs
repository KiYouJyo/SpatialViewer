using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace SpatialViewer.Product;

internal sealed record GitHubReleaseAsset(string Name, string BrowserDownloadUrl, long Size, string? Digest);

internal sealed record GitHubReleaseInfo(
    string TagName,
    string HtmlUrl,
    string? Name,
    string? Body,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<GitHubReleaseAsset> Assets)
{
    public string DisplayVersion => TagName.TrimStart('v', 'V');
}

internal static class GitHubUpdateService
{
    private static readonly HttpClient Client = CreateApiClient(TimeSpan.FromSeconds(15));
    private static readonly HttpClient DownloadClient = CreateDownloadClient(TimeSpan.FromMinutes(5));

    public static async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string repository, CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync($"https://api.github.com/repos/{repository}/releases/latest", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = json.RootElement;
        var assets = root.TryGetProperty("assets", out var assetArray) && assetArray.ValueKind == JsonValueKind.Array
            ? assetArray.EnumerateArray().Select(ParseAsset).Where(static asset => asset is not null).Cast<GitHubReleaseAsset>().ToArray()
            : [];
        return new GitHubReleaseInfo(
            root.GetProperty("tag_name").GetString() ?? string.Empty,
            root.GetProperty("html_url").GetString() ?? $"https://github.com/{repository}/releases",
            root.TryGetProperty("name", out var name) ? name.GetString() : null,
            root.TryGetProperty("body", out var body) ? body.GetString() : null,
            root.TryGetProperty("published_at", out var published) && published.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(published.GetString(), out var timestamp) ? timestamp : null,
            assets);
    }

    public static bool IsNewer(string availableTag, Version current) =>
        TryParseVersionTag(availableTag, out var available) && available > current;

    public static bool TryParseVersionTag(string tag, out Version version)
    {
        var normalized = tag.Trim().TrimStart('v', 'V');
        var separator = normalized.IndexOfAny(['-', '+']);
        if (separator >= 0) normalized = normalized[..separator];
        return Version.TryParse(normalized, out version!);
    }

    public static async Task DownloadAssetAsync(
        GitHubReleaseAsset asset,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (!Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri) || downloadUri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("The release asset URL is invalid.");
        if (!TryGetSha256Digest(asset.Digest, out var expectedDigest))
            throw new InvalidDataException("The release asset does not provide a SHA-256 digest.");

        var directory = Path.GetDirectoryName(destinationPath) ?? throw new InvalidDataException("The destination directory is invalid.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{destinationPath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        try
        {
            using var response = await DownloadClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var expectedLength = response.Content.Headers.ContentLength ?? asset.Size;
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    total += read;
                    if (expectedLength > 0) progress?.Report(Math.Clamp(total / (double)expectedLength, 0d, 1d));
                }
            }

            await using var verificationStream = File.OpenRead(temporaryPath);
            var actualDigest = await SHA256.HashDataAsync(verificationStream, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(actualDigest, expectedDigest))
                throw new InvalidDataException("The release asset SHA-256 digest does not match GitHub metadata.");

            File.Move(temporaryPath, destinationPath, overwrite: true);
            progress?.Report(1d);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static GitHubReleaseAsset? ParseAsset(JsonElement asset)
    {
        var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
        var url = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) return null;
        var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize) ? parsedSize : 0;
        var digest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null;
        return new GitHubReleaseAsset(name, url, size, digest);
    }

    private static bool TryGetSha256Digest(string? digest, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            bytes = Convert.FromHexString(digest[7..]);
            return bytes.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static HttpClient CreateApiClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SpatialViewer", "0.2.1"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static HttpClient CreateDownloadClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SpatialViewer", "0.2.1"));
        return client;
    }
}

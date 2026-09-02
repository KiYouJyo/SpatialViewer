using Windows.Storage;

namespace SpatialViewer.Product;

internal sealed record ProductUpdateDownloadResult(
    bool Succeeded,
    string? PackagePath = null,
    string? ErrorCode = null,
    string? ErrorDetail = null);

internal static class ProductUpdateService
{
    public static async Task<ProductUpdateDownloadResult> DownloadAsync(
        GitHubReleaseInfo release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);

        var asset = FindPackageAsset(release);
        if (asset is null)
        {
            return new ProductUpdateDownloadResult(
                false,
                ErrorCode: "MissingAsset",
                ErrorDetail: $"The v{release.DisplayVersion} release does not contain the expected x64 MSIX bundle.");
        }

        var directory = Path.Combine(GetUpdateRoot(), SanitizeSegment(release.DisplayVersion));
        Directory.CreateDirectory(directory);
        var packagePath = Path.Combine(directory, asset.Name);

        try
        {
            await GitHubUpdateService.DownloadAssetAsync(asset, packagePath, progress, cancellationToken).ConfigureAwait(false);
            return new ProductUpdateDownloadResult(true, packagePath);
        }
        catch (GitHubAssetDownloadException exception)
        {
            return new ProductUpdateDownloadResult(false, packagePath, exception.Code, exception.Message);
        }
    }

    internal static GitHubReleaseAsset? FindPackageAsset(GitHubReleaseInfo release)
    {
        if (!GitHubUpdateService.TryParseVersionTag(release.TagName, out var version)) return null;
        var expectedName = $"SpatialViewer_{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}.0_x64.msixbundle";
        return release.Assets.FirstOrDefault(asset => string.Equals(asset.Name, expectedName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetUpdateRoot()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "ProductUpdates");
        }
        catch (InvalidOperationException)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SpatialViewer",
                "ProductUpdates");
        }
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }
}

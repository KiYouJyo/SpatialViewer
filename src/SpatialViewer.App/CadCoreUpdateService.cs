using System.IO.Compression;

namespace SpatialViewer.Product;

internal enum CadCoreUpdateState
{
    NotChecked,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    Verifying,
    ReadyForRestart,
    Failed
}

internal sealed record CadCoreUpdateProgress(CadCoreUpdateState State, double? Fraction = null);

internal sealed record CadCoreUpdateResult(
    CadCoreUpdateState State,
    Version CurrentVersion,
    Version? AvailableVersion = null,
    string? ErrorCode = null);

internal sealed class CadCoreUpdateService
{
    private const string Repository = "KiYouJyo/SpatialViewer.CadCore";
    private GitHubReleaseInfo? _pendingRelease;
    private GitHubReleaseAsset? _pendingAsset;
    private Version? _pendingVersion;

    private static Version CurrentVersion => CadCoreRuntimeBootstrapper.CurrentVersion;

    public async Task<CadCoreUpdateResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var current = CurrentVersion;
        try
        {
            var release = await GitHubUpdateService.GetLatestReleaseAsync(Repository, cancellationToken).ConfigureAwait(false);
            if (release is null) return new(CadCoreUpdateState.Failed, current, ErrorCode: "NoRelease");
            if (!GitHubUpdateService.TryParseVersionTag(release.TagName, out var parsedVersion))
                return new(CadCoreUpdateState.Failed, current, ErrorCode: "InvalidVersion");

            var available = CadCoreRuntimeBootstrapper.NormalizeVersion(parsedVersion);
            _pendingRelease = release;
            _pendingVersion = available;
            _pendingAsset = FindKernelAsset(release);

            if (available <= current) return new(CadCoreUpdateState.UpToDate, current, available);
            if (_pendingAsset is null) return new(CadCoreUpdateState.Failed, current, available, "MissingAsset");
            if (CadCoreRuntimeBootstrapper.IsPendingVersion(available))
                return new(CadCoreUpdateState.ReadyForRestart, current, available);
            return new(CadCoreUpdateState.UpdateAvailable, current, available);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(CadCoreUpdateState.Failed, current, ErrorCode: "Timeout");
        }
        catch (HttpRequestException)
        {
            return new(CadCoreUpdateState.Failed, current, ErrorCode: "Network");
        }
    }

    public async Task<CadCoreUpdateResult> DownloadAndStageAsync(
        IProgress<CadCoreUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var current = CurrentVersion;
        if (_pendingRelease is null || _pendingAsset is null || _pendingVersion is null)
            return new(CadCoreUpdateState.Failed, current, ErrorCode: "NoPendingUpdate");

        var available = _pendingVersion;
        if (available <= current) return new(CadCoreUpdateState.UpToDate, current, available);
        var downloadsRoot = Path.Combine(CadCoreRuntimeBootstrapper.KernelRoot, "downloads");
        var archivePath = Path.Combine(downloadsRoot, $"CadCore-v{CadCoreRuntimeBootstrapper.FormatVersion(available)}-x64.zip");
        var finalDirectory = CadCoreRuntimeBootstrapper.GetVersionDirectory(available);
        var temporaryDirectory = $"{finalDirectory}.staging-{Environment.ProcessId}-{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(downloadsRoot);
            progress?.Report(new(CadCoreUpdateState.Downloading, 0d));
            var downloadProgress = new Progress<double>(fraction => progress?.Report(new(CadCoreUpdateState.Downloading, fraction)));
            await GitHubUpdateService.DownloadAssetAsync(_pendingAsset, archivePath, downloadProgress, cancellationToken).ConfigureAwait(false);

            progress?.Report(new(CadCoreUpdateState.Verifying));
            Directory.CreateDirectory(temporaryDirectory);
            ExtractSafely(archivePath, temporaryDirectory, cancellationToken);
            if (!CadCorePackageValidator.TryValidate(temporaryDirectory, out var package, out var validationError) || package is null)
                return new(CadCoreUpdateState.Failed, current, available, validationError ?? "InvalidPackage");
            if (package.Version != available)
                return new(CadCoreUpdateState.Failed, current, available, "VersionMismatch");

            if (Directory.Exists(finalDirectory)) Directory.Delete(finalDirectory, recursive: true);
            Directory.Move(temporaryDirectory, finalDirectory);
            CadCoreRuntimeBootstrapper.StageForNextLaunch(available);
            progress?.Report(new(CadCoreUpdateState.ReadyForRestart, 1d));
            return new(CadCoreUpdateState.ReadyForRestart, current, available);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or HttpRequestException)
        {
            return new(CadCoreUpdateState.Failed, current, available, exception.Message);
        }
        finally
        {
            TryDeleteDirectory(temporaryDirectory);
            TryDeleteFile(archivePath);
        }
    }

    private static GitHubReleaseAsset? FindKernelAsset(GitHubReleaseInfo release)
    {
        var expectedName = $"CadCore-v{release.DisplayVersion}-x64.zip";
        return release.Assets.FirstOrDefault(asset => string.Equals(asset.Name, expectedName, StringComparison.OrdinalIgnoreCase));
    }

    private static void ExtractSafely(string archivePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        var rootWithSeparator = destinationRoot.EndsWith(Path.DirectorySeparatorChar)
            ? destinationRoot
            : destinationRoot + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var targetPath = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
            if (!targetPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) && !string.Equals(targetPath, destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The CadCore archive contains a path outside the staging directory.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? destinationRoot);
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

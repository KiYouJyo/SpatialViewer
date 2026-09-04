using System.Text.Json;
using SpatialViewer.Core;

namespace SpatialViewer.Presentation;

public sealed class RecentFilesService
{
    private const int MaximumCount = 30;
    private readonly string _storagePath;
    public RecentFilesService(string storagePath) => _storagePath = storagePath;

    public async Task<IReadOnlyList<RecentFile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storagePath)) return Array.Empty<RecentFile>();
        await using var stream = File.OpenRead(_storagePath);
        var stored = await JsonSerializer.DeserializeAsync<List<RecentFile>>(stream, cancellationToken: cancellationToken).ConfigureAwait(false) ?? [];
        return stored.Select(item => item with { Exists = File.Exists(item.Path) }).OrderByDescending(item => item.LastOpenedUtc).Take(MaximumCount).ToArray();
    }

    public async Task RecordAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!FormatGate.IsSupported(path)) return;
        var current = (await LoadAsync(cancellationToken).ConfigureAwait(false)).Where(item => !string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)).ToList();
        var file = new FileInfo(path);
        current.Insert(0, new RecentFile(
            path,
            file.Name,
            file.Extension,
            ResolveDocumentKind(file.Extension),
            DateTimeOffset.UtcNow,
            file.Exists ? file.Length : 0,
            file.Exists));
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
        await using var stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, current.Take(MaximumCount).ToArray(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static DocumentKind ResolveDocumentKind(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".dwg" or ".dxf" => DocumentKind.Cad,
            ".3dm" => DocumentKind.Rhino,
            ".gpkg" or ".shp" or ".tif" or ".tiff" or ".geojson" or ".json" => DocumentKind.Gis,
            ".ifc" => DocumentKind.Bim,
            _ => DocumentKind.Synthetic,
        };
}

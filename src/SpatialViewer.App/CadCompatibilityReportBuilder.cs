using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;

namespace SpatialViewer.Product;

internal static class CadCompatibilityReportBuilder
{
    public const int SchemaVersion = 1;

    private static readonly string[] SafeDocumentMetadataKeys =
    [
        "Reader",
        "ReaderVersion",
        "SourceFormat",
        "CadVersion",
        "Units",
        "CustomClassCount",
        "CustomEntityCount",
        "CustomProxyGraphicEntityCount",
        "XiangyuanDetected",
        "XiangyuanClassCount",
        "XiangyuanEntityCount",
        "RawProxyCommandCaptureSupported",
        "RawProxyCommandCaptureFailed",
        "RawProxyCommandCapturedEntityCount",
        "RawProxyCommandMalformedEntityCount",
        "RawProxyUnknownCommandEntityCount",
        "RawProxyUnknownCommandCount",
        "RawProxyUnknownTypeIds"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Build(CadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var customEntities = EnumerateEntities(document)
            .OfType<CadCustomEntity>()
            .ToArray();

        var groups = customEntities
            .GroupBy(EntityGroupKey.From)
            .OrderBy(group => group.Key.Vendor, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ApplicationName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.CppClassName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.DxfName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Key.SourceEntityType, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildGroup(group.Key, group.ToArray()))
            .ToArray();

        var report = new CadCompatibilityReport(
            SchemaVersion,
            DateTimeOffset.UtcNow,
            AppVersionProvider.Version,
            typeof(CadDocument).Assembly.GetName().Version?.ToString() ?? "unknown",
            typeof(ACadSharpCadImporter).Assembly.GetName().Version?.ToString() ?? "unknown",
            document.SourceFormat,
            document.Version,
            document.Units.ToString(),
            document.CustomClasses.Count,
            customEntities.Length,
            FilterMetadata(document.Metadata, SafeDocumentMetadataKeys),
            groups);

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static CadCompatibilityCustomGroup BuildGroup(
        EntityGroupKey key,
        IReadOnlyList<CadCustomEntity> entities)
    {
        var primitiveKinds = entities
            .SelectMany(entity => entity.ProxyGraphicKinds)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var commandSignatures = DistinctMetadata(entities, "RawProxyCommandTypeSignature");
        var unknownTypeIds = entities
            .SelectMany(entity => ParseIntegerList(Metadata(entity, "RawProxyUnknownTypeIds")))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        return new(
            key.DxfName,
            key.CppClassName,
            key.ApplicationName,
            key.SourceEntityType,
            key.Vendor,
            key.Representation,
            entities.Count,
            primitiveKinds,
            SumMetadata(entities, "ProxyGraphicCount"),
            SumMetadata(entities, "ProxyGraphicTranslatedCount"),
            SumMetadata(entities, "ProxyGraphicUnsupportedCount"),
            SumMetadata(entities, "RawProxyCommandDeclaredCount"),
            SumMetadata(entities, "RawProxyCommandScannedCount"),
            SumMetadata(entities, "RawProxyCommandKnownCount"),
            SumMetadata(entities, "RawProxyCommandUnknownCount"),
            entities.Count(entity => MetadataBoolean(entity, "RawProxyCommandMalformed")),
            entities.Count(entity => MetadataBoolean(entity, "RawProxyCommandTruncated")),
            unknownTypeIds,
            commandSignatures);
    }

    private static IEnumerable<CadEntity> EnumerateEntities(CadDocument document)
    {
        foreach (var entity in document.ModelSpace) yield return entity;
        foreach (var block in document.Blocks)
            foreach (var entity in block.Entities)
                yield return entity;
        foreach (var layout in document.Layouts.Where(layout => layout.IsPaperSpace))
            foreach (var entity in layout.Entities)
                yield return entity;
    }

    private static IReadOnlyDictionary<string, string> FilterMetadata(
        IReadOnlyDictionary<string, string> source,
        IEnumerable<string> allowList)
    {
        var safe = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in allowList)
            if (source.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                safe[key] = value;
        return new ReadOnlyDictionary<string, string>(safe);
    }

    private static string[] DistinctMetadata(
        IEnumerable<CadCustomEntity> entities,
        string key)
        => entities
            .Select(entity => Metadata(entity, key))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string Metadata(CadCustomEntity entity, string key)
        => entity.Metadata.TryGetValue(key, out var value) ? value : string.Empty;

    private static bool MetadataBoolean(CadCustomEntity entity, string key)
        => bool.TryParse(Metadata(entity, key), out var value) && value;

    private static long SumMetadata(
        IEnumerable<CadCustomEntity> entities,
        string key)
    {
        long sum = 0;
        foreach (var entity in entities)
            if (long.TryParse(Metadata(entity, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
                sum = checked(sum + value);
        return sum;
    }

    private static IEnumerable<int> ParseIntegerList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        foreach (var token in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                yield return value;
    }

    private sealed record EntityGroupKey(
        string DxfName,
        string CppClassName,
        string ApplicationName,
        string SourceEntityType,
        string Vendor,
        string Representation)
    {
        public static EntityGroupKey From(CadCustomEntity entity)
            => new(
                entity.ClassDefinition?.DxfName ?? string.Empty,
                entity.ClassDefinition?.CppClassName ?? string.Empty,
                entity.ClassDefinition?.ApplicationName ?? string.Empty,
                entity.SourceEntityType,
                entity.Vendor.ToString(),
                entity.Representation.ToString());
    }
}

internal sealed record CadCompatibilityReport(
    int SchemaVersion,
    DateTimeOffset GeneratedUtc,
    string AppVersion,
    string CadCoreAssemblyVersion,
    string CadAdapterAssemblyVersion,
    string SourceFormat,
    string CadVersion,
    string Units,
    int CustomClassCount,
    int CustomEntityCount,
    IReadOnlyDictionary<string, string> AggregateMetadata,
    IReadOnlyList<CadCompatibilityCustomGroup> CustomGroups);

internal sealed record CadCompatibilityCustomGroup(
    string DxfName,
    string CppClassName,
    string ApplicationName,
    string SourceEntityType,
    string Vendor,
    string Representation,
    int EntityCount,
    IReadOnlyList<string> ProxyGraphicKinds,
    long ProxyGraphicCommandCount,
    long ProxyGraphicTranslatedCount,
    long ProxyGraphicUnsupportedCount,
    long RawCommandDeclaredCount,
    long RawCommandScannedCount,
    long RawCommandKnownCount,
    long RawCommandUnknownCount,
    int RawCommandMalformedEntityCount,
    int RawCommandTruncatedEntityCount,
    IReadOnlyList<int> RawUnknownTypeIds,
    IReadOnlyList<string> RawCommandTypeSignatures);

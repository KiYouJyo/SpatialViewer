using SpatialViewer.Core;

namespace SpatialViewer.Presentation;

public sealed record RecentFile(string Path, string DisplayName, string Extension, DocumentKind DocumentKind, DateTimeOffset LastOpenedUtc, long FileSize, bool Exists);
public sealed record PropertyRow(string Label, string Value);
public sealed record PropertySection(string Name, IReadOnlyList<PropertyRow> Rows);
public sealed record DiagnosticGroup(DiagnosticSeverity Severity, string Code, string Message, int Count);

public static class FormatGate
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase) { ".dwg", ".dxf", ".3dm" };
    public static bool IsSupported(string path) => Supported.Contains(Path.GetExtension(path));
    public static string UnsupportedMessage(string path) => $"{Path.GetExtension(path).ToUpperInvariant()} is not supported yet. Spatial Viewer currently opens DWG, DXF and Rhino 3DM files.";
}

public static class DiagnosticsPresenter
{
    public static IReadOnlyList<DiagnosticGroup> Aggregate(IEnumerable<Diagnostic> diagnostics) => diagnostics
        .GroupBy(diagnostic => new { diagnostic.Severity, diagnostic.Code, diagnostic.Message })
        .Select(group => new DiagnosticGroup(group.Key.Severity, group.Key.Code, group.Key.Message, group.Count()))
        .OrderByDescending(group => group.Severity).ThenBy(group => group.Code, StringComparer.Ordinal)
        .ToArray();
}

public static class PropertyPresenter
{
    public static IReadOnlyList<PropertySection> Create(SceneItem? selection)
    {
        if (selection is not { } item) return Array.Empty<PropertySection>();
        var objectRows = new List<PropertyRow>
        {
            new("Type", item.Geometry.GetType().Name.Replace("Geometry", string.Empty, StringComparison.Ordinal)),
            new("Layer", item.Layer.Name),
            new("Color", item.Style.Stroke),
            new("Line type", item.Metadata.TryGetValue("LineType", out var lineType) ? lineType : "—")
        };
        var geometryRows = new List<PropertyRow>
        {
            new("Bounds", $"{item.Bounds.MinX:G6}, {item.Bounds.MinY:G6} — {item.Bounds.MaxX:G6}, {item.Bounds.MaxY:G6}"),
            new("Closed", item.Geometry is PolygonGeometry or PolylineGeometry { IsClosed: true } or PathGeometry { IsClosed: true } ? "Yes" : "No")
        };
        return new[] { new PropertySection("Object", objectRows), new PropertySection("Geometry", geometryRows) };
    }
}

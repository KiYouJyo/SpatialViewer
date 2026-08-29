using System.Diagnostics;
using SpatialViewer.Core;
using SpatialViewer.Rendering;
using SpatialViewer.Formats.Cad.ACadSharp;

var sizes = new[] { 10_000, 100_000, 1_000_000 };
Console.WriteLine("Spatial Viewer Stage 1 baseline (Release, elapsed milliseconds)");
foreach (var count in sizes)
{
    var creation = Stopwatch.StartNew(); var document = SyntheticScenes.Stress(count); creation.Stop();
    var bounds = Stopwatch.StartNew(); var sceneBounds = document.Scene.GetBounds(); bounds.Stop();
    var camera = new Camera2D(sceneBounds.Center); var prep = Stopwatch.StartNew(); var frame = RenderPreparation.Prepare(document.Scene, camera); prep.Stop();
    var hit = Stopwatch.StartNew(); var selected = HitTesting.HitTest(document.Scene, sceneBounds.Center, 5); hit.Stop();
    Console.WriteLine($"{count:N0}: create={creation.Elapsed.TotalMilliseconds:F1} bounds={bounds.Elapsed.TotalMilliseconds:F1} prepare={prep.Elapsed.TotalMilliseconds:F1} hit={hit.Elapsed.TotalMilliseconds:F1} commands={frame.Commands.Count} selected={(selected.HasValue ? "yes" : "no")}");
}

Console.WriteLine("CAD import baseline (Release, elapsed milliseconds)");
foreach (var count in new[] { 10_000, 100_000 })
{
    var path = Path.Combine(Path.GetTempPath(), $"spatial-viewer-benchmark-{count}.dxf");
    try
    {
        var content = new System.Text.StringBuilder("0\nSECTION\n2\nENTITIES\n");
        for (var index = 0; index < count; index++) content.Append("0\nLINE\n8\n0\n10\n").Append(index).Append("\n20\n0\n11\n").Append(index + 1).Append("\n21\n1\n");
        content.Append("0\nENDSEC\n0\nEOF\n"); File.WriteAllText(path, content.ToString());
        var import = Stopwatch.StartNew(); var result = await new ACadSharpCadImporter().ImportAsync(new SpatialViewer.Core.ImportRequest(path)); import.Stop();
        var document = result.Document ?? throw new InvalidOperationException("CAD benchmark import failed."); var preparation = Stopwatch.StartNew(); var frame = RenderPreparation.Prepare(document.Scene, new Camera2D(document.Bounds.Center)); preparation.Stop();
        Console.WriteLine($"CAD {count:N0}: import={import.Elapsed.TotalMilliseconds:F1} prepare={preparation.Elapsed.TotalMilliseconds:F1} commands={frame.Commands.Count}");
    }
    finally { if (File.Exists(path)) File.Delete(path); }
}

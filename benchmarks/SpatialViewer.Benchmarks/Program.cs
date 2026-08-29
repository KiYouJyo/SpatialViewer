using System.Diagnostics;
using SpatialViewer.Core;
using SpatialViewer.Rendering;

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

using SpatialViewer.Core;
using SpatialViewer.Rendering;

namespace SpatialViewer.IntegrationTests;

public sealed class SyntheticIntegrationTests
{
    [Theory]
    [InlineData(10_000)]
    [InlineData(100_000)]
    public void StressScenePreparesWithoutLosingObjects(int count) { var document = SyntheticScenes.Stress(count); var camera = new Camera2D(document.Bounds.Center); Assert.Equal(count, RenderPreparation.Prepare(document.Scene, camera).Commands.Count); }
    [Fact] public void LargeCoordinateScreenDeltasRemainResolvable() { var camera = new Camera2D(new(500000, 3400000), 100); var viewport = new Size2D(1000, 800); var a = camera.WorldToScreen(new(500000.001, 3400000.001), viewport); var b = camera.WorldToScreen(new(500000.002, 3400000.001), viewport); Assert.InRange(b.X - a.X, .099, .101); }
}

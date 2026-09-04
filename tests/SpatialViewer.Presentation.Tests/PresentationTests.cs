using SpatialViewer.Core;
using SpatialViewer.Presentation;

namespace SpatialViewer.Presentation.Tests;

public sealed class PresentationTests
{
    [Fact]
    public void WorkspaceFocusesDuplicatePathAndClosesWithoutAffectingOtherSession()
    {
        var workspace = new DocumentWorkspace();
        var first = workspace.OpenOrFocus("first.dxf", out var firstDuplicate);
        var second = workspace.OpenOrFocus("second.dwg", out var secondDuplicate);
        var same = workspace.OpenOrFocus("first.dxf", out var sameDuplicate);
        Assert.False(firstDuplicate); Assert.False(secondDuplicate); Assert.True(sameDuplicate); Assert.Same(first, same); Assert.Equal(2, workspace.Documents.Count);
        Assert.True(workspace.Close(first)); Assert.Single(workspace.Documents); Assert.Same(second, workspace.ActiveDocument);
        workspace.CloseAll();
    }

    [Theory]
    [InlineData("drawing.dwg", true)]
    [InlineData("drawing.dxf", true)]
    [InlineData("drawing.ifc", false)]
    [InlineData("drawing.3dm", false)]
    public void FormatGateOnlyAdmitsImplementedCadFormats(string path, bool allowed) => Assert.Equal(allowed, FormatGate.IsSupported(path));

    [Fact]
    public void DiagnosticsAggregateSameCode()
    {
        var groups = DiagnosticsPresenter.Aggregate(new[] { new Diagnostic(DiagnosticSeverity.Warning, "CAD_UNSUPPORTED_ENTITY", "HATCH"), new Diagnostic(DiagnosticSeverity.Warning, "CAD_UNSUPPORTED_ENTITY", "HATCH"), new Diagnostic(DiagnosticSeverity.Warning, "CAD_UNSUPPORTED_ENTITY", "DIMENSION") });
        Assert.Equal(2, groups.Count); Assert.Contains(groups, group => group.Message == "HATCH" && group.Count == 2);
    }

    [Fact]
    public void PropertiesUseRealSceneMetadataWithoutCadReaderTypes()
    {
        var layer = new Layer("roads", "Roads");
        var item = new SceneItem(ObjectId.New(), new LineGeometry(new Point2D(0, 0), new Point2D(10, 0)), Transform2D.Identity, new SceneStyle("#FFFFFF"), layer, new BoundingBox2D(0, 0, 10, 0), new Dictionary<string, string> { ["LineType"] = "Continuous" });
        var properties = PropertyPresenter.Create(item);
        Assert.Contains(properties.SelectMany(section => section.Rows), row => row.Label == "Layer" && row.Value == "Roads");
        Assert.Empty(PropertyPresenter.Create(null));
    }

    [Fact]
    public async Task RecentFilesPersistAndMarkMissingFiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"spatial-viewer-presentation-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "sample.dxf"); Directory.CreateDirectory(folder); await File.WriteAllTextAsync(path, "fixture");
        try
        {
            var service = new RecentFilesService(Path.Combine(folder, "recent.json"));
            await service.RecordAsync(path);
            Assert.True((await service.LoadAsync()).Single().Exists);
            File.Delete(path);
            Assert.False((await service.LoadAsync()).Single().Exists);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task LoadingDocumentDoesNotImplicitlyFitOrResetCamera()
    {
        using var session = new DocumentSession("camera-contract.dxf");
        session.Camera.SetTarget(new Point2D(1234.5, -678.25));
        session.Camera.SetZoom(3.75);

        await session.LoadAsync(new StubImporter(SyntheticScenes.BasicPrimitives()));

        Assert.Equal(DocumentSessionState.Ready, session.State);
        Assert.Equal(new Point2D(1234.5, -678.25), session.Camera.Target);
        Assert.Equal(3.75, session.Camera.Zoom);
    }

    private sealed class StubImporter(IDocument document) : IDocumentImporter
    {
        public bool CanImport(string filePath) => true;

        public Task<ImportResult> ImportAsync(ImportRequest request, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImportResult(document, Array.Empty<Diagnostic>()));
    }
    [Fact]
    public void FormatGateSupportsThreeDmAlongsideCad()
    {
        Assert.True(FormatGate.IsSupported("model.dwg"));
        Assert.True(FormatGate.IsSupported("model.dxf"));
        Assert.True(FormatGate.IsSupported("model.3dm"));
        Assert.False(FormatGate.IsSupported("model.ifc"));
    }
}

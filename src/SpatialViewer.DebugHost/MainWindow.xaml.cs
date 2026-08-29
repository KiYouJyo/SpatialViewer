using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SpatialViewer.Core;
using SpatialViewer.Rendering;
using SpatialViewer.Rendering.Windows;
using SpatialViewer.Formats.Cad;
using SpatialViewer.Formats.Cad.ACadSharp;
using Windows.Foundation;
using Windows.Storage.Pickers;
using System.Diagnostics;

namespace SpatialViewer.DebugHost;
public sealed partial class MainWindow : Window, IDisposable
{
    private readonly Camera2D _camera = new(Point2D.Origin);
    private readonly Win2DSceneRenderer _renderer;
    private IDocument _document = SyntheticScenes.BasicPrimitives();
    private ObjectId? _selection;
    private Point? _panStart;
    private bool _panMoved;
    private bool _initialized;
    public MainWindow() { InitializeComponent(); _renderer = new Win2DSceneRenderer(ViewportCanvas); _renderer.FrameRendered += milliseconds => FpsText.Text = $"Frame: {milliseconds:F2} ms ({(milliseconds > 0 ? 1000 / milliseconds : 0):F1} FPS)"; Closed += (_, _) => Dispose(); _initialized = true; LoadScene(_document); }
    public void Dispose() => _renderer.Dispose();
    private Size2D Size => new(Math.Max(1, ViewportCanvas.ActualWidth), Math.Max(1, ViewportCanvas.ActualHeight));
    private void LoadScene(IDocument document) { _document = document; _selection = null; LayerList.ItemsSource = _document.Layers; DiagnosticsText.Text = DescribeDocument(document); _camera.Fit(_document.Bounds, Size); Draw(); }
    private void Draw() { var frame = RenderPreparation.Prepare(_document.Scene, _camera); _renderer.Render(frame, _camera, Size, _selection); ObjectsText.Text = $"Objects: {frame.Commands.Count:N0}"; ZoomText.Text = $"Zoom: {_camera.Zoom:G5}"; SelectionText.Text = _selection is { } value ? $"Selection: {value}" : "Selection: none"; }
    private void Fit_Click(object sender, RoutedEventArgs e) { _camera.Fit(_document.Bounds, Size); Draw(); }
    private void Reset_Click(object sender, RoutedEventArgs e) { _camera.SetTarget(Point2D.Origin); _camera.SetZoom(1); Draw(); }
    private void ScenePicker_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_initialized) return; LoadScene(ScenePicker.SelectedIndex switch { 1 => SyntheticScenes.NestedTransforms(), 2 => SyntheticScenes.LargeCoordinates(), 3 => SyntheticScenes.Stress(100_000), 4 => SyntheticScenes.Stress(1_000_000), _ => SyntheticScenes.BasicPrimitives() }); }
    private async void OpenCad_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".dxf"); picker.FileTypeFilter.Add(".dwg"); WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync(); if (file is null) return;
        try { DiagnosticsText.Text = "Importing CAD…"; var watch = Stopwatch.StartNew(); var result = await new ACadSharpCadImporter().ImportAsync(new ImportRequest(file.Path)); watch.Stop(); if (result.Document is null) { DiagnosticsText.Text = $"Import failed: {string.Join("; ", result.Diagnostics.Select(x => x.Code))}"; return; } LoadScene(result.Document); DiagnosticsText.Text += $" | Import {watch.Elapsed.TotalMilliseconds:F1} ms"; }
        catch (OperationCanceledException) { DiagnosticsText.Text = "Import cancelled"; }
        catch (Exception exception) { DiagnosticsText.Text = $"Import failed: {exception.Message}"; }
    }
    private void Layer_Click(object sender, RoutedEventArgs e) => Draw();
    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e) => Draw();
    private void Viewport_PointerWheelChanged(object sender, PointerRoutedEventArgs e) { var current = e.GetCurrentPoint(ViewportCanvas); var point = current.Position; _camera.ZoomAt(current.Properties.MouseWheelDelta > 0 ? 1.2 : 1 / 1.2, new(point.X, point.Y), Size); Draw(); }
    private void Viewport_PointerPressed(object sender, PointerRoutedEventArgs e) { var point = e.GetCurrentPoint(ViewportCanvas); if (point.Properties.IsLeftButtonPressed) { _panStart = point.Position; _panMoved = false; ViewportCanvas.CapturePointer(e.Pointer); } else { Select(point.Position); } }
    private void Viewport_PointerMoved(object sender, PointerRoutedEventArgs e) { var point = e.GetCurrentPoint(ViewportCanvas).Position; CoordinateText.Text = $"World: {_camera.ScreenToWorld(new(point.X, point.Y), Size).X:F2}, {_camera.ScreenToWorld(new(point.X, point.Y), Size).Y:F2}"; if (_panStart is { } start && e.GetCurrentPoint(ViewportCanvas).Properties.IsLeftButtonPressed) { var delta = new Vector2D(point.X - start.X, point.Y - start.Y); if (delta.Length > .5) { _panMoved = true; _camera.PanScreen(delta); _panStart = point; Draw(); } } }
    private void Viewport_PointerReleased(object sender, PointerRoutedEventArgs e) { var point = e.GetCurrentPoint(ViewportCanvas).Position; if (_panStart is not null) { _panStart = null; ViewportCanvas.ReleasePointerCaptures(); if (!_panMoved) Select(point); } else Select(point); }
    private void Select(Point point) { var hit = HitTesting.HitTest(_document.Scene, _camera.ScreenToWorld(new(point.X, point.Y), Size), 6 / _camera.Zoom); _selection = hit?.Id; SelectionText.Text = hit is { } item ? $"Selection: {item.Id}\n{string.Join(" | ", item.Geometry.GetType().Name, item.Layer.Name, item.Geometry.GetBounds())}\n{string.Join(" | ", item.Metadata.Select(pair => $"{pair.Key}: {pair.Value}"))}" : "Selection: none"; Draw(); }
    private static string DescribeDocument(IDocument document) => document is CadDocument cad ? $"{cad.SourceFormat} {cad.Version} | units {cad.Units} | entities {cad.ModelSpace.Count} | layers {cad.CadLayers.Count} | blocks {cad.Blocks.Count} | diagnostics {cad.Diagnostics.Count}" : $"Diagnostics: {document.Diagnostics.Count}";
}

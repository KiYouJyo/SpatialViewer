using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SpatialViewer.Core;
using SpatialViewer.Presentation;
using SpatialViewer.Rendering;
using SpatialViewer.Rendering.Windows;
using Windows.Foundation;

namespace SpatialViewer.Product.Controls;

public enum ViewerMode { Select, Pan }

public sealed partial class CadViewportControl : UserControl, IDisposable
{
    private Win2DSceneRenderer? _renderer;
    private DocumentSession? _session;
    private Point? _panStart;
    private Point2D? _panAnchorWorld;
    private uint? _capturedPointerId;
    private bool _panMoved;
    private bool _disposed;
    public event EventHandler<SceneItem?>? SelectionChanged;
    public event EventHandler<Point2D>? PointerWorldChanged;
    public ViewerMode Mode { get; set; } = ViewerMode.Pan;
    public DocumentSession? Session { get => _session; set { _session = value; Draw(); } }

    public CadViewportControl()
    {
        InitializeComponent();
        Loaded += (_, _) => { _renderer ??= CreateRenderer(); Draw(); };
        Unloaded += (_, _) => DisposeRenderer();
    }

    public void Fit() { if (_session?.Document is { } document) { _session.Camera.Fit(document.Bounds, Size); Draw(); } }
    public void Draw()
    {
        if (_renderer is null || _session?.Document is not { } document || _session.State != DocumentSessionState.Ready) return;
        _renderer.Render(RenderPreparation.Prepare(document.Scene, _session.Camera), _session.Camera, Size, _session.Selection);
    }

    private Win2DSceneRenderer CreateRenderer() => new(ViewportCanvas) { CanvasColor = "#000000", SelectionColor = "#42B8E3" };
    private Size2D Size => new(Math.Max(1, ViewportCanvas.ActualWidth), Math.Max(1, ViewportCanvas.ActualHeight));
    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e) => Draw();
    private void Viewport_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_session is null) return;
        var point = e.GetCurrentPoint(ViewportCanvas);
        _session.Camera.ZoomAt(point.Properties.MouseWheelDelta > 0 ? 1.2 : 1 / 1.2, new Point2D(point.Position.X, point.Position.Y), Size);
        Draw();
    }
    private void Viewport_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_session is null) return;
        var point = e.GetCurrentPoint(ViewportCanvas);
        if (point.Properties.IsMiddleButtonPressed || (Mode == ViewerMode.Pan && point.Properties.IsLeftButtonPressed))
        {
            _panStart = point.Position;
            _panAnchorWorld = _session.Camera.ScreenToWorld(new Point2D(point.Position.X, point.Position.Y), Size);
            _capturedPointerId = e.Pointer.PointerId;
            _panMoved = false;
            ViewportCanvas.CapturePointer(e.Pointer);
        }
        else if (Mode == ViewerMode.Select && point.Properties.IsLeftButtonPressed)
        {
            _panStart = point.Position;
            _panAnchorWorld = null;
            _capturedPointerId = e.Pointer.PointerId;
            _panMoved = false;
            ViewportCanvas.CapturePointer(e.Pointer);
        }
    }
    private void Viewport_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_session is null) return;
        var point = e.GetCurrentPoint(ViewportCanvas); var position = point.Position;
        PointerWorldChanged?.Invoke(this, _session.Camera.ScreenToWorld(new Point2D(position.X, position.Y), Size));
        if (_panStart is { } start && _panAnchorWorld is { } anchor && _capturedPointerId == e.Pointer.PointerId)
        {
            var delta = new Vector2D(position.X - start.X, position.Y - start.Y);
            if (delta.Length > .5)
            {
                _panMoved = true;
                var size = Size;
                _session.Camera.SetTarget(new Point2D(
                    anchor.X - ((position.X - size.Width / 2) / _session.Camera.Zoom),
                    anchor.Y + ((position.Y - size.Height / 2) / _session.Camera.Zoom)));
                Draw();
            }
        }
    }
    private void Viewport_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_session is null) return;
        var position = e.GetCurrentPoint(ViewportCanvas).Position;
        if (_panStart is not null && _capturedPointerId == e.Pointer.PointerId)
        {
            _panStart = null;
            _panAnchorWorld = null;
            _capturedPointerId = null;
            ViewportCanvas.ReleasePointerCaptures();
            if (!_panMoved && Mode == ViewerMode.Select) Select(position);
        }
    }
    private void Viewport_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_capturedPointerId != e.Pointer.PointerId) return;
        _panStart = null;
        _panAnchorWorld = null;
        _capturedPointerId = null;
        _panMoved = false;
        ViewportCanvas.ReleasePointerCaptures();
    }
    private void Select(Point position)
    {
        if (_session?.Document is not { } document) return;
        var hit = HitTesting.HitTest(document.Scene, _session.Camera.ScreenToWorld(new Point2D(position.X, position.Y), Size), 6 / _session.Camera.Zoom);
        _session.Selection = hit?.Id;
        SelectionChanged?.Invoke(this, hit);
        Draw();
    }
    private void DisposeRenderer() { _renderer?.Dispose(); _renderer = null; }
    public void Dispose() { if (_disposed) return; _disposed = true; DisposeRenderer(); }
}

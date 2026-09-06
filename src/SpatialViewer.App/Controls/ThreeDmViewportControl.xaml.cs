using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Integration;
using SpatialViewer.ThreeDm.Rendering;
using Windows.Foundation;
using Windows.UI;

namespace SpatialViewer.Product.Controls;

internal enum ThreeDmViewerMode { Select, Orbit, Pan }

public sealed partial class ThreeDmViewportControl : UserControl, IDisposable
{
    private ThreeDmProductSession? _session;
    private ThreeDmCameraState? _camera;
    private Point? _pointerStart;
    private ThreeDmCameraState? _pointerStartCamera;
    private uint? _capturedPointerId;
    private bool _panning;
    private bool _pointerMoved;
    private bool _disposed;
    private string _canvasColor = "#000000";
    private readonly Dictionary<int, (int A, int B)[]> _wireEdgeCache = [];

    public ThreeDmViewportControl()
    {
        InitializeComponent();
    }

    internal event EventHandler<ThreeDmSelectionProperties?>? SelectionChanged;
    internal ThreeDmViewerMode Mode { get; set; } = ThreeDmViewerMode.Orbit;

    internal ThreeDmProductSession? Session
    {
        get => _session;
        set
        {
            _session = value;
            _wireEdgeCache.Clear();
            if (_session?.State == ThreeDmProductSessionState.Ready) Fit();
            Draw();
        }
    }

    public string CanvasColor
    {
        get => _canvasColor;
        set
        {
            var normalized = string.Equals(value, "#FFFFFF", StringComparison.OrdinalIgnoreCase)
                ? "#FFFFFF"
                : "#000000";
            if (_canvasColor == normalized) return;
            _canvasColor = normalized;
            ViewportBackground.Background = new SolidColorBrush(
                normalized == "#FFFFFF" ? Colors.White : Colors.Black);
            Draw();
        }
    }

    public ThreeDmCameraState? Camera => _camera;

    public void Fit()
    {
        if (_session?.State != ThreeDmProductSessionState.Ready) return;
        var preset = _session.ViewPresets.FirstOrDefault(item => item.Key == "standard:perspective")
            ?? (_session.ViewPresets.Count > 0 ? _session.ViewPresets[0] : null);
        if (preset is null) return;
        SetView(preset.Camera);
    }

    public void SetView(ThreeDmCameraState camera)
    {
        _camera = camera;
        Draw();
    }

    public void Draw()
    {
        if (_disposed) return;
        ViewportCanvas.Invalidate();
    }

    private void ViewportCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        args.DrawingSession.Clear(ParseCanvasColor());
        if (_session?.State != ThreeDmProductSessionState.Ready ||
            _session.RenderScene is not { } scene ||
            _camera is not { } camera)
        {
            return;
        }

        var width = Math.Max(1, sender.ActualWidth);
        var height = Math.Max(1, sender.ActualHeight);
        var aspect = width / height;
        var basis = CameraBasis.Create(camera);
        var policies = scene.MeshDrawPolicies.ToDictionary(item => item.GeometryIndex);
        var geometries = scene.SharedMeshes.Geometries.ToDictionary(item => item.GeometryIndex);

        var fillTrianglesRemaining = 80_000;
        var wireSegmentsRemaining = 120_000;
        var curveSegmentsRemaining = 100_000;
        var pointsRemaining = 50_000;

        var batches = new List<ProjectedMeshBatch>(scene.SharedMeshes.Instances.Count);
        foreach (var instance in scene.SharedMeshes.Instances)
        {
            if (!geometries.TryGetValue(instance.GeometryIndex, out var geometry))
            {
                continue;
            }

            policies.TryGetValue(instance.GeometryIndex, out var policy);
            policy ??= new ThreeDmPreparedMeshDrawPolicy(instance.GeometryIndex, true, false);
            var center = Center(geometry.Bounds);
            var worldCenter = TransformPoint(center, instance.Transform);
            var depth = Dot(Subtract(worldCenter, camera.Location), basis.Forward);
            batches.Add(new ProjectedMeshBatch(instance, geometry, policy, depth));
        }

        foreach (var batch in batches.OrderByDescending(item => item.Depth))
        {
            var projected = ProjectVertices(
                batch.Geometry,
                batch.Instance.Transform,
                camera,
                basis,
                aspect,
                width,
                height);
            var triangleCount = batch.Geometry.Indices.Count / 3;
            var drawFill = batch.Policy.DrawFill &&
                triangleCount > 0 &&
                triangleCount <= fillTrianglesRemaining;
            var color = ResolveDisplayColor(
                batch.Instance.Appearance.ColorArgb,
                batch.Instance.Appearance.Opacity);

            if (drawFill)
            {
                DrawFilledMesh(
                    args.DrawingSession,
                    batch.Geometry,
                    projected,
                    color);
                fillTrianglesRemaining -= triangleCount;
            }

            if (batch.Policy.DrawWireIndices || !drawFill)
            {
                DrawWireMesh(
                    args.DrawingSession,
                    batch.Geometry,
                    projected,
                    ResolveDisplayColor(
                        batch.Instance.Appearance.ColorArgb,
                        Math.Max(batch.Instance.Appearance.Opacity, 0.72)),
                    ref wireSegmentsRemaining);
            }
        }

        foreach (var curve in scene.Curves)
        {
            if (curveSegmentsRemaining <= 0)
            {
                break;
            }

            DrawCurve(
                args.DrawingSession,
                curve,
                camera,
                basis,
                aspect,
                width,
                height,
                ResolveDisplayColor(curve.Appearance.ColorArgb, curve.Appearance.Opacity),
                ref curveSegmentsRemaining);
        }

        foreach (var pointSet in scene.PointSets)
        {
            if (pointsRemaining <= 0)
            {
                break;
            }

            var color = ResolveDisplayColor(pointSet.Appearance.ColorArgb, pointSet.Appearance.Opacity);
            var stride = Math.Max(1, (int)Math.Ceiling((double)pointSet.Points.Count / Math.Max(1, pointsRemaining)));
            for (var index = 0; index < pointSet.Points.Count && pointsRemaining > 0; index += stride)
            {
                if (Project(ToPoint(pointSet.Points[index]), camera, basis, aspect, width, height, out var projected))
                {
                    args.DrawingSession.FillCircle(projected.Screen, 2.5f, color);
                    pointsRemaining--;
                }
            }
        }

        DrawSelectionOverlay(args.DrawingSession, scene, camera, basis, aspect, width, height);
    }

    private static ProjectedPoint?[] ProjectVertices(
        ThreeDmSharedMeshGeometry geometry,
        Transform3d transform,
        ThreeDmCameraState camera,
        CameraBasis basis,
        double aspect,
        double width,
        double height)
    {
        var projected = new ProjectedPoint?[geometry.Vertices.Count];
        for (var index = 0; index < geometry.Vertices.Count; index++)
        {
            if (!TryVertex(geometry, index, transform, out var point) ||
                !Project(point, camera, basis, aspect, width, height, out var screen))
            {
                continue;
            }

            projected[index] = screen;
        }

        return projected;
    }

    private static void DrawFilledMesh(
        CanvasDrawingSession drawingSession,
        ThreeDmSharedMeshGeometry geometry,
        IReadOnlyList<ProjectedPoint?> projected,
        Color color)
    {
        using var path = new CanvasPathBuilder(drawingSession);
        var hasFigures = false;
        for (var index = 0; index + 2 < geometry.Indices.Count; index += 3)
        {
            var aIndex = geometry.Indices[index];
            var bIndex = geometry.Indices[index + 1];
            var cIndex = geometry.Indices[index + 2];
            if ((uint)aIndex >= (uint)projected.Count ||
                (uint)bIndex >= (uint)projected.Count ||
                (uint)cIndex >= (uint)projected.Count ||
                projected[aIndex] is not { } a ||
                projected[bIndex] is not { } b ||
                projected[cIndex] is not { } c)
            {
                continue;
            }

            path.BeginFigure(a.Screen);
            path.AddLine(b.Screen);
            path.AddLine(c.Screen);
            path.EndFigure(CanvasFigureLoop.Closed);
            hasFigures = true;
        }

        if (!hasFigures)
        {
            return;
        }

        using var geometryPath = CanvasGeometry.CreatePath(path);
        drawingSession.FillGeometry(geometryPath, color);
    }

    private void DrawWireMesh(
        CanvasDrawingSession drawingSession,
        ThreeDmSharedMeshGeometry geometry,
        IReadOnlyList<ProjectedPoint?> projected,
        Color color,
        ref int segmentBudget)
    {
        if (segmentBudget <= 0 || geometry.Indices.Count < 3)
        {
            return;
        }

        var triangleCount = geometry.Indices.Count / 3;
        var desiredTriangles = Math.Max(1, segmentBudget / 3);
        var stride = Math.Max(1, (int)Math.Ceiling((double)triangleCount / desiredTriangles));
        using var path = new CanvasPathBuilder(drawingSession);
        var hasFigures = false;

        for (var triangle = 0; triangle < triangleCount && segmentBudget >= 3; triangle += stride)
        {
            var index = triangle * 3;
            var aIndex = geometry.Indices[index];
            var bIndex = geometry.Indices[index + 1];
            var cIndex = geometry.Indices[index + 2];
            if ((uint)aIndex >= (uint)projected.Count ||
                (uint)bIndex >= (uint)projected.Count ||
                (uint)cIndex >= (uint)projected.Count ||
                projected[aIndex] is not { } a ||
                projected[bIndex] is not { } b ||
                projected[cIndex] is not { } c)
            {
                continue;
            }

            AddLineFigure(path, a.Screen, b.Screen);
            AddLineFigure(path, b.Screen, c.Screen);
            AddLineFigure(path, c.Screen, a.Screen);
            hasFigures = true;
            segmentBudget -= 3;
        }

        if (!hasFigures)
        {
            return;
        }

        using var geometryPath = CanvasGeometry.CreatePath(path);
        drawingSession.DrawGeometry(geometryPath, color, 1f);
    }

    private static void DrawCurve(
        CanvasDrawingSession drawingSession,
        ThreeDmRenderCurve curve,
        ThreeDmCameraState camera,
        CameraBasis basis,
        double aspect,
        double width,
        double height,
        Color color,
        ref int segmentBudget)
    {
        if (curve.Points.Count < 2 || segmentBudget <= 0)
        {
            return;
        }

        var segmentCount = curve.Points.Count - 1 + (curve.IsClosed ? 1 : 0);
        var stride = Math.Max(1, (int)Math.Ceiling((double)segmentCount / Math.Max(1, segmentBudget)));
        using var path = new CanvasPathBuilder(drawingSession);
        var hasFigures = false;

        for (var index = 1; index < curve.Points.Count && segmentBudget > 0; index += stride)
        {
            var previous = Math.Max(0, index - 1);
            if (!Project(ToPoint(curve.Points[previous]), camera, basis, aspect, width, height, out var a) ||
                !Project(ToPoint(curve.Points[index]), camera, basis, aspect, width, height, out var b))
            {
                continue;
            }

            AddLineFigure(path, a.Screen, b.Screen);
            hasFigures = true;
            segmentBudget--;
        }

        if (curve.IsClosed && curve.Points.Count > 2 && segmentBudget > 0 &&
            Project(ToPoint(curve.Points[^1]), camera, basis, aspect, width, height, out var last) &&
            Project(ToPoint(curve.Points[0]), camera, basis, aspect, width, height, out var first))
        {
            AddLineFigure(path, last.Screen, first.Screen);
            hasFigures = true;
            segmentBudget--;
        }

        if (!hasFigures)
        {
            return;
        }

        using var geometryPath = CanvasGeometry.CreatePath(path);
        drawingSession.DrawGeometry(geometryPath, color, 1f);
    }

    private static void AddLineFigure(CanvasPathBuilder path, Vector2 start, Vector2 end)
    {
        path.BeginFigure(start);
        path.AddLine(end);
        path.EndFigure(CanvasFigureLoop.Open);
    }

    private (int A, int B)[] GetWireEdges(ThreeDmSharedMeshGeometry geometry)
    {
        if (_wireEdgeCache.TryGetValue(geometry.GeometryIndex, out var cached)) return cached;
        var edges = new HashSet<(int A, int B)>();
        for (var index = 0; index + 2 < geometry.Indices.Count; index += 3)
        {
            AddEdge(geometry.Indices[index], geometry.Indices[index + 1], edges);
            AddEdge(geometry.Indices[index + 1], geometry.Indices[index + 2], edges);
            AddEdge(geometry.Indices[index + 2], geometry.Indices[index], edges);
        }

        cached = edges.OrderBy(item => item.A).ThenBy(item => item.B).ToArray();
        _wireEdgeCache[geometry.GeometryIndex] = cached;
        return cached;
    }

    private static void AddEdge(int left, int right, HashSet<(int A, int B)> edges)
    {
        edges.Add(left <= right ? (left, right) : (right, left));
    }

    private static bool TryVertex(
        ThreeDmSharedMeshGeometry geometry,
        int index,
        Transform3d transform,
        out Point3d point)
    {
        if ((uint)index >= (uint)geometry.Vertices.Count)
        {
            point = default;
            return false;
        }

        var source = geometry.Vertices[index];
        var x = (transform.M00 * source.X) + (transform.M01 * source.Y) + (transform.M02 * source.Z) + transform.M03;
        var y = (transform.M10 * source.X) + (transform.M11 * source.Y) + (transform.M12 * source.Z) + transform.M13;
        var z = (transform.M20 * source.X) + (transform.M21 * source.Y) + (transform.M22 * source.Z) + transform.M23;
        var w = (transform.M30 * source.X) + (transform.M31 * source.Y) + (transform.M32 * source.Z) + transform.M33;
        if (Math.Abs(w) > 1e-15 && Math.Abs(w - 1) > 1e-15)
        {
            x /= w;
            y /= w;
            z /= w;
        }

        point = new Point3d(x, y, z);
        return double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z);
    }

    private static Point3d ToPoint(ThreeDmRenderVertex point) => new(point.X, point.Y, point.Z);

    private static bool Project(
        Point3d point,
        ThreeDmCameraState camera,
        CameraBasis basis,
        double aspect,
        double width,
        double height,
        out ProjectedPoint result)
    {
        var delta = Subtract(point, camera.Location);
        var x = Dot(delta, basis.Right);
        var y = Dot(delta, basis.Up);
        var z = Dot(delta, basis.Forward);
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
        {
            result = default;
            return false;
        }

        double ndcX;
        double ndcY;
        if (camera.Projection == ThreeDmCameraProjection.Perspective)
        {
            if (z <= Math.Max(1e-9, camera.NearPlaneDistance * 0.25))
            {
                result = default;
                return false;
            }

            if (camera.SourceFrustum is { } frustum)
            {
                var nearX = x * frustum.Near / z;
                var nearY = y * frustum.Near / z;
                ndcX = (2 * (nearX - frustum.Left) / (frustum.Right - frustum.Left)) - 1;
                ndcY = (2 * (nearY - frustum.Bottom) / (frustum.Top - frustum.Bottom)) - 1;
            }
            else
            {
                var tanHalf = Math.Tan(camera.VerticalFieldOfViewRadians * 0.5);
                if (!(tanHalf > 0) || !double.IsFinite(tanHalf))
                {
                    result = default;
                    return false;
                }

                ndcX = x / (z * tanHalf * aspect);
                ndcY = y / (z * tanHalf);
            }
        }
        else
        {
            if (camera.SourceFrustum is { } frustum)
            {
                ndcX = (2 * (x - frustum.Left) / (frustum.Right - frustum.Left)) - 1;
                ndcY = (2 * (y - frustum.Bottom) / (frustum.Top - frustum.Bottom)) - 1;
            }
            else
            {
                var halfHeight = Math.Max(camera.OrthographicHeight * 0.5, 1e-9);
                ndcX = x / (halfHeight * aspect);
                ndcY = y / halfHeight;
            }
        }

        if (!double.IsFinite(ndcX) || !double.IsFinite(ndcY) ||
            Math.Abs(ndcX) > 100 || Math.Abs(ndcY) > 100)
        {
            result = default;
            return false;
        }

        result = new ProjectedPoint(
            new Vector2(
                (float)((ndcX + 1) * 0.5 * width),
                (float)((1 - ndcY) * 0.5 * height)),
            z);
        return true;
    }

    private void Viewport_SizeChanged(object sender, SizeChangedEventArgs e) => Draw();

    private void Viewport_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_camera is not { } camera) return;
        var delta = e.GetCurrentPoint(ViewportCanvas).Properties.MouseWheelDelta;
        if (camera.Projection == ThreeDmCameraProjection.Orthographic)
        {
            var factor = delta > 0 ? 0.85 : 1.0 / 0.85;
            _camera = camera with
            {
                OrthographicHeight = Math.Max(camera.OrthographicHeight * factor, 1e-9),
                SourceFrustum = null,
            };
        }
        else
        {
            var offset = Subtract(camera.Location, camera.Target);
            var factor = delta > 0 ? 0.85 : 1.0 / 0.85;
            _camera = camera with
            {
                Location = Add(camera.Target, ScaleVector(offset, factor)),
                SourceFrustum = null,
            };
        }

        Draw();
    }

    private void Viewport_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_camera is null) return;
        var point = e.GetCurrentPoint(ViewportCanvas);
        if (!point.Properties.IsLeftButtonPressed && !point.Properties.IsMiddleButtonPressed) return;

        _pointerStart = point.Position;
        _pointerStartCamera = _camera;
        _capturedPointerId = e.Pointer.PointerId;
        _panning = point.Properties.IsMiddleButtonPressed ||
            (Mode == ThreeDmViewerMode.Pan && point.Properties.IsLeftButtonPressed);
        _pointerMoved = false;
        ViewportCanvas.CapturePointer(e.Pointer);
    }

    private void Viewport_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerStart is not { } start ||
            _pointerStartCamera is not { } camera ||
            _capturedPointerId != e.Pointer.PointerId)
        {
            return;
        }

        var position = e.GetCurrentPoint(ViewportCanvas).Position;
        var dx = position.X - start.X;
        var dy = position.Y - start.Y;
        if (Math.Abs(dx) > 0.5 || Math.Abs(dy) > 0.5) _pointerMoved = true;
        if (Mode == ThreeDmViewerMode.Select && !_panning) return;

        var basis = CameraBasis.Create(camera);

        if (_panning)
        {
            var viewportHeight = Math.Max(1, ViewportCanvas.ActualHeight);
            var distance = Length(Subtract(camera.Location, camera.Target));
            var worldPerPixel = camera.Projection == ThreeDmCameraProjection.Orthographic
                ? Math.Max(camera.OrthographicHeight, 1e-9) / viewportHeight
                : Math.Max(2 * distance * Math.Tan(camera.VerticalFieldOfViewRadians * 0.5), 1e-9) / viewportHeight;
            var translation = Add(
                ScaleVector(basis.Right, -dx * worldPerPixel),
                ScaleVector(basis.Up, dy * worldPerPixel));
            _camera = camera with
            {
                Location = Add(camera.Location, translation),
                Target = Add(camera.Target, translation),
                SourceFrustum = null,
            };
        }
        else
        {
            var offset = Subtract(camera.Location, camera.Target);
            var yawed = Rotate(offset, basis.Up, -dx * 0.006);
            var right = Normalize(Cross(ScaleVector(yawed, -1), camera.Up));
            if (Length(right) <= 1e-12) right = basis.Right;
            var rotated = Rotate(yawed, right, -dy * 0.006);
            var up = Normalize(Rotate(camera.Up, right, -dy * 0.006));
            _camera = camera with
            {
                Location = Add(camera.Target, rotated),
                Up = Length(up) > 1e-12 ? up : camera.Up,
                SourceFrustum = null,
            };
        }

        Draw();
    }

    private void Viewport_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var position = e.GetCurrentPoint(ViewportCanvas).Position;
        var shouldSelect = _capturedPointerId == e.Pointer.PointerId &&
            Mode == ThreeDmViewerMode.Select &&
            !_panning &&
            !_pointerMoved;
        EndPointer(e.Pointer.PointerId);
        if (shouldSelect) Select(position);
    }

    private void Viewport_PointerCanceled(object sender, PointerRoutedEventArgs e) => EndPointer(e.Pointer.PointerId);

    private void EndPointer(uint pointerId)
    {
        if (_capturedPointerId != pointerId) return;
        _pointerStart = null;
        _pointerStartCamera = null;
        _capturedPointerId = null;
        _pointerMoved = false;
        ViewportCanvas.ReleasePointerCaptures();
    }

    private void Select(Point position)
    {
        if (_session?.State != ThreeDmProductSessionState.Ready ||
            _session.RenderScene is not { } scene ||
            _camera is not { } camera)
        {
            return;
        }

        var width = Math.Max(1, ViewportCanvas.ActualWidth);
        var height = Math.Max(1, ViewportCanvas.ActualHeight);
        var aspect = width / height;
        var basis = CameraBasis.Create(camera);
        var pointer = new Vector2((float)position.X, (float)position.Y);
        var geometries = scene.SharedMeshes.Geometries.ToDictionary(item => item.GeometryIndex);
        ThreeDmSelectionId? best = null;
        var bestDistance = double.MaxValue;
        var bestDepth = double.MaxValue;

        void Consider(ThreeDmSelectionId id, double distance, double depth)
        {
            if (distance > bestDistance + 1e-6) return;
            if (Math.Abs(distance - bestDistance) <= 1e-6 && depth >= bestDepth) return;
            best = id;
            bestDistance = distance;
            bestDepth = depth;
        }

        foreach (var instance in scene.SharedMeshes.Instances)
        {
            if (!geometries.TryGetValue(instance.GeometryIndex, out var geometry)) continue;
            for (var index = 0; index + 2 < geometry.Indices.Count; index += 3)
            {
                if (!TryVertex(geometry, geometry.Indices[index], instance.Transform, out var a) ||
                    !TryVertex(geometry, geometry.Indices[index + 1], instance.Transform, out var b) ||
                    !TryVertex(geometry, geometry.Indices[index + 2], instance.Transform, out var d) ||
                    !Project(a, camera, basis, aspect, width, height, out var pa) ||
                    !Project(b, camera, basis, aspect, width, height, out var pb) ||
                    !Project(d, camera, basis, aspect, width, height, out var pd) ||
                    !PointInTriangle(pointer, pa.Screen, pb.Screen, pd.Screen))
                {
                    continue;
                }

                Consider(
                    ThreeDmSelectionId.Create(instance.SourceObjectId, instance.SourceSubobjectIndex, instance.InstancePath),
                    0,
                    (pa.Depth + pb.Depth + pd.Depth) / 3);
            }
        }

        const double pickRadius = 6;
        foreach (var curve in scene.Curves)
        {
            for (var index = 1; index < curve.Points.Count; index++)
            {
                if (!Project(ToPoint(curve.Points[index - 1]), camera, basis, aspect, width, height, out var pa) ||
                    !Project(ToPoint(curve.Points[index]), camera, basis, aspect, width, height, out var pb))
                {
                    continue;
                }

                var distance = DistanceToSegment(pointer, pa.Screen, pb.Screen);
                if (distance <= pickRadius)
                {
                    Consider(
                        ThreeDmSelectionId.Create(curve.SourceObjectId, curve.SourceSubobjectIndex, curve.InstancePath),
                        distance,
                        Math.Min(pa.Depth, pb.Depth));
                }
            }
        }

        foreach (var pointSet in scene.PointSets)
        {
            foreach (var point in pointSet.Points)
            {
                if (!Project(ToPoint(point), camera, basis, aspect, width, height, out var projected)) continue;
                var distance = Vector2.Distance(pointer, projected.Screen);
                if (distance <= pickRadius)
                {
                    Consider(
                        ThreeDmSelectionId.Create(pointSet.SourceObjectId, null, pointSet.InstancePath),
                        distance,
                        projected.Depth);
                }
            }
        }

        _session.Selection = best;
        SelectionChanged?.Invoke(this, best is { } id ? _session.GetSelectionProperties(id) : null);
        Draw();
    }

    private void DrawSelectionOverlay(
        CanvasDrawingSession drawingSession,
        ThreeDmPreparedRenderScene scene,
        ThreeDmCameraState camera,
        CameraBasis basis,
        double aspect,
        double width,
        double height)
    {
        if (_session?.Selection is not { } selection) return;
        var geometries = scene.SharedMeshes.Geometries.ToDictionary(item => item.GeometryIndex);
        var highlight = Color.FromArgb(255, 0x42, 0xB8, 0xE3);

        foreach (var instance in scene.SharedMeshes.Instances)
        {
            var id = ThreeDmSelectionId.Create(instance.SourceObjectId, instance.SourceSubobjectIndex, instance.InstancePath);
            if (id != selection || !geometries.TryGetValue(instance.GeometryIndex, out var geometry)) continue;
            foreach (var (aIndex, bIndex) in GetWireEdges(geometry))
            {
                if (!TryVertex(geometry, aIndex, instance.Transform, out var a) ||
                    !TryVertex(geometry, bIndex, instance.Transform, out var b) ||
                    !Project(a, camera, basis, aspect, width, height, out var pa) ||
                    !Project(b, camera, basis, aspect, width, height, out var pb))
                {
                    continue;
                }

                drawingSession.DrawLine(pa.Screen, pb.Screen, highlight, 2f);
            }
        }

        foreach (var curve in scene.Curves)
        {
            var id = ThreeDmSelectionId.Create(curve.SourceObjectId, curve.SourceSubobjectIndex, curve.InstancePath);
            if (id != selection) continue;
            for (var index = 1; index < curve.Points.Count; index++)
            {
                if (Project(ToPoint(curve.Points[index - 1]), camera, basis, aspect, width, height, out var pa) &&
                    Project(ToPoint(curve.Points[index]), camera, basis, aspect, width, height, out var pb))
                {
                    drawingSession.DrawLine(pa.Screen, pb.Screen, highlight, 2.5f);
                }
            }
        }

        foreach (var pointSet in scene.PointSets)
        {
            var id = ThreeDmSelectionId.Create(pointSet.SourceObjectId, null, pointSet.InstancePath);
            if (id != selection) continue;
            foreach (var point in pointSet.Points)
            {
                if (Project(ToPoint(point), camera, basis, aspect, width, height, out var projected))
                    drawingSession.FillCircle(projected.Screen, 4f, highlight);
            }
        }
    }

    private static bool PointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
            ((p1.X - p3.X) * (p2.Y - p3.Y)) - ((p2.X - p3.X) * (p1.Y - p3.Y));
        var d1 = Sign(point, a, b);
        var d2 = Sign(point, b, c);
        var d3 = Sign(point, c, a);
        var hasNegative = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPositive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNegative && hasPositive);
    }

    private static double DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var delta = end - start;
        var lengthSquared = delta.LengthSquared();
        if (lengthSquared <= float.Epsilon) return Vector2.Distance(point, start);
        var t = Math.Clamp(Vector2.Dot(point - start, delta) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, start + (delta * t));
    }

    private Color ParseCanvasColor() => _canvasColor == "#FFFFFF" ? Colors.White : Colors.Black;

    private Color ResolveDisplayColor(uint argb, double opacity)
    {
        var alpha = (byte)((argb >> 24) & 0xFF);
        var red = (byte)((argb >> 16) & 0xFF);
        var green = (byte)((argb >> 8) & 0xFF);
        var blue = (byte)(argb & 0xFF);
        var luminance = ((0.2126 * red) + (0.7152 * green) + (0.0722 * blue)) / 255.0;

        if (_canvasColor == "#000000" && luminance < 0.18)
        {
            var amount = luminance < 0.06 ? 0.78 : 0.58;
            red = Blend(red, 235, amount);
            green = Blend(green, 235, amount);
            blue = Blend(blue, 235, amount);
        }
        else if (_canvasColor == "#FFFFFF" && luminance > 0.86)
        {
            red = Blend(red, 35, 0.72);
            green = Blend(green, 35, 0.72);
            blue = Blend(blue, 35, 0.72);
        }

        var combinedAlpha = (byte)Math.Clamp(alpha * Math.Clamp(opacity, 0, 1), 0, 255);
        return Color.FromArgb(combinedAlpha, red, green, blue);
    }

    private static byte Blend(byte source, byte target, double amount) =>
        (byte)Math.Clamp(Math.Round(source + ((target - source) * amount)), 0, 255);

    private static Vector3d Subtract(Point3d left, Point3d right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    private static Point3d Add(Point3d point, Vector3d vector) =>
        new(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);

    private static Point3d Center(BoundingBox3d bounds) =>
        bounds.IsValid
            ? new Point3d(
                bounds.Min.X + ((bounds.Max.X - bounds.Min.X) * 0.5),
                bounds.Min.Y + ((bounds.Max.Y - bounds.Min.Y) * 0.5),
                bounds.Min.Z + ((bounds.Max.Z - bounds.Min.Z) * 0.5))
            : new Point3d(0, 0, 0);

    private static Point3d TransformPoint(Point3d source, Transform3d transform)
    {
        var x = (transform.M00 * source.X) + (transform.M01 * source.Y) + (transform.M02 * source.Z) + transform.M03;
        var y = (transform.M10 * source.X) + (transform.M11 * source.Y) + (transform.M12 * source.Z) + transform.M13;
        var z = (transform.M20 * source.X) + (transform.M21 * source.Y) + (transform.M22 * source.Z) + transform.M23;
        var w = (transform.M30 * source.X) + (transform.M31 * source.Y) + (transform.M32 * source.Z) + transform.M33;
        if (Math.Abs(w) > 1e-15 && Math.Abs(w - 1.0) > 1e-15)
        {
            x /= w;
            y /= w;
            z /= w;
        }

        return new Point3d(x, y, z);
    }

    private static Vector3d Add(Vector3d left, Vector3d right) =>
        new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    private static Vector3d ScaleVector(Vector3d vector, double factor) =>
        new(vector.X * factor, vector.Y * factor, vector.Z * factor);

    private static double Dot(Vector3d left, Vector3d right) =>
        (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private static Vector3d Cross(Vector3d left, Vector3d right) =>
        new(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X));

    private static double Length(Vector3d vector) =>
        Math.Sqrt(Dot(vector, vector));

    private static Vector3d Normalize(Vector3d vector)
    {
        var length = Length(vector);
        return length > 1e-15 ? ScaleVector(vector, 1 / length) : new Vector3d(0, 0, 0);
    }

    private static Vector3d Rotate(Vector3d vector, Vector3d axis, double radians)
    {
        axis = Normalize(axis);
        if (Length(axis) <= 1e-15) return vector;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return Add(
            Add(
                ScaleVector(vector, cosine),
                ScaleVector(Cross(axis, vector), sine)),
            ScaleVector(axis, Dot(axis, vector) * (1 - cosine)));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _wireEdgeCache.Clear();
    }

    private readonly record struct ProjectedPoint(Vector2 Screen, double Depth);
    private readonly record struct ProjectedMeshBatch(
        ThreeDmSharedMeshInstance Instance,
        ThreeDmSharedMeshGeometry Geometry,
        ThreeDmPreparedMeshDrawPolicy Policy,
        double Depth);

    private readonly record struct CameraBasis(Vector3d Forward, Vector3d Right, Vector3d Up)
    {
        public static CameraBasis Create(ThreeDmCameraState camera)
        {
            var forward = Normalize(Subtract(camera.Target, camera.Location));
            var up = Normalize(camera.Up);
            var right = Normalize(Cross(forward, up));
            if (Length(right) <= 1e-15)
            {
                right = Normalize(Cross(forward, new Vector3d(0, 0, 1)));
                if (Length(right) <= 1e-15) right = new Vector3d(1, 0, 0);
            }

            up = Normalize(Cross(right, forward));
            return new CameraBasis(forward, right, up);
        }
    }
}

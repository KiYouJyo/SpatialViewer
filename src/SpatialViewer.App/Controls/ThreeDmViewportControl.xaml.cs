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

public sealed partial class ThreeDmViewportControl : UserControl, IDisposable
{
    private ThreeDmProductSession? _session;
    private ThreeDmCameraState? _camera;
    private Point? _pointerStart;
    private ThreeDmCameraState? _pointerStartCamera;
    private uint? _capturedPointerId;
    private bool _panning;
    private bool _disposed;
    private string _canvasColor = "#000000";
    private readonly Dictionary<int, (int A, int B)[]> _wireEdgeCache = [];

    public ThreeDmViewportControl()
    {
        InitializeComponent();
    }

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
            ?? _session.ViewPresets.FirstOrDefault();
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
        var fills = new List<ProjectedTriangle>();

        foreach (var instance in scene.SharedMeshes.Instances)
        {
            if (!geometries.TryGetValue(instance.GeometryIndex, out var geometry)) continue;
            policies.TryGetValue(instance.GeometryIndex, out var policy);
            policy ??= new ThreeDmPreparedMeshDrawPolicy(instance.GeometryIndex, true, false);
            var color = ToColor(instance.Appearance.ColorArgb, instance.Appearance.Opacity);

            if (policy.DrawFill)
            {
                for (var index = 0; index + 2 < geometry.Indices.Count; index += 3)
                {
                    var aIndex = geometry.Indices[index];
                    var bIndex = geometry.Indices[index + 1];
                    var cIndex = geometry.Indices[index + 2];
                    if (!TryVertex(geometry, aIndex, instance.Transform, out var a) ||
                        !TryVertex(geometry, bIndex, instance.Transform, out var b) ||
                        !TryVertex(geometry, cIndex, instance.Transform, out var c) ||
                        !Project(a, camera, basis, aspect, width, height, out var pa) ||
                        !Project(b, camera, basis, aspect, width, height, out var pb) ||
                        !Project(c, camera, basis, aspect, width, height, out var pc))
                    {
                        continue;
                    }

                    fills.Add(new ProjectedTriangle(
                        pa.Screen,
                        pb.Screen,
                        pc.Screen,
                        (pa.Depth + pb.Depth + pc.Depth) / 3,
                        color));
                }
            }
        }

        foreach (var triangle in fills.OrderByDescending(item => item.Depth))
        {
            FillTriangle(args.DrawingSession, triangle);
        }

        foreach (var instance in scene.SharedMeshes.Instances)
        {
            if (!geometries.TryGetValue(instance.GeometryIndex, out var geometry)) continue;
            if (!policies.TryGetValue(instance.GeometryIndex, out var policy) || !policy.DrawWireIndices) continue;
            var color = ToColor(instance.Appearance.ColorArgb, Math.Max(instance.Appearance.Opacity, 0.65));
            foreach (var (aIndex, bIndex) in GetWireEdges(geometry))
            {
                if (!TryVertex(geometry, aIndex, instance.Transform, out var a) ||
                    !TryVertex(geometry, bIndex, instance.Transform, out var b) ||
                    !Project(a, camera, basis, aspect, width, height, out var pa) ||
                    !Project(b, camera, basis, aspect, width, height, out var pb))
                {
                    continue;
                }

                args.DrawingSession.DrawLine(pa.Screen, pb.Screen, color, 1f);
            }
        }

        foreach (var curve in scene.Curves)
        {
            var color = ToColor(curve.Appearance.ColorArgb, curve.Appearance.Opacity);
            for (var index = 1; index < curve.Points.Count; index++)
            {
                var a = ToPoint(curve.Points[index - 1]);
                var b = ToPoint(curve.Points[index]);
                if (!Project(a, camera, basis, aspect, width, height, out var pa) ||
                    !Project(b, camera, basis, aspect, width, height, out var pb))
                {
                    continue;
                }

                args.DrawingSession.DrawLine(pa.Screen, pb.Screen, color, 1f);
            }

            if (curve.IsClosed && curve.Points.Count > 2)
            {
                var a = ToPoint(curve.Points[^1]);
                var b = ToPoint(curve.Points[0]);
                if (Project(a, camera, basis, aspect, width, height, out var pa) &&
                    Project(b, camera, basis, aspect, width, height, out var pb))
                {
                    args.DrawingSession.DrawLine(pa.Screen, pb.Screen, color, 1f);
                }
            }
        }

        foreach (var pointSet in scene.PointSets)
        {
            var color = ToColor(pointSet.Appearance.ColorArgb, pointSet.Appearance.Opacity);
            foreach (var point in pointSet.Points)
            {
                if (Project(ToPoint(point), camera, basis, aspect, width, height, out var projected))
                {
                    args.DrawingSession.FillCircle(projected.Screen, 2.5f, color);
                }
            }
        }
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

    private static void FillTriangle(CanvasDrawingSession session, ProjectedTriangle triangle)
    {
        using var path = new CanvasPathBuilder(session);
        path.BeginFigure(triangle.A);
        path.AddLine(triangle.B);
        path.AddLine(triangle.C);
        path.EndFigure(CanvasFigureLoop.Closed);
        using var geometry = CanvasGeometry.CreatePath(path);
        session.FillGeometry(geometry, triangle.Color);
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
        _panning = point.Properties.IsMiddleButtonPressed;
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

    private void Viewport_PointerReleased(object sender, PointerRoutedEventArgs e) => EndPointer(e.Pointer.PointerId);
    private void Viewport_PointerCanceled(object sender, PointerRoutedEventArgs e) => EndPointer(e.Pointer.PointerId);

    private void EndPointer(uint pointerId)
    {
        if (_capturedPointerId != pointerId) return;
        _pointerStart = null;
        _pointerStartCamera = null;
        _capturedPointerId = null;
        ViewportCanvas.ReleasePointerCaptures();
    }

    private Color ParseCanvasColor() => _canvasColor == "#FFFFFF" ? Colors.White : Colors.Black;

    private static Color ToColor(uint argb, double opacity)
    {
        var alpha = (byte)((argb >> 24) & 0xFF);
        var red = (byte)((argb >> 16) & 0xFF);
        var green = (byte)((argb >> 8) & 0xFF);
        var blue = (byte)(argb & 0xFF);
        var combinedAlpha = (byte)Math.Clamp(alpha * Math.Clamp(opacity, 0, 1), 0, 255);
        return Color.FromArgb(combinedAlpha, red, green, blue);
    }

    private static Vector3d Subtract(Point3d left, Point3d right) =>
        new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    private static Point3d Add(Point3d point, Vector3d vector) =>
        new(point.X + vector.X, point.Y + vector.Y, point.Z + vector.Z);

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
    private readonly record struct ProjectedTriangle(Vector2 A, Vector2 B, Vector2 C, double Depth, Color Color);

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

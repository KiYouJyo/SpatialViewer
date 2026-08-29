# Rendering decision: Win2D over Direct2D for Stage 1

The Windows backend is a real `CanvasControl` renderer using Win2D 1.4.0, which is a maintained .NET projection over Direct2D and DirectWrite and is MIT licensed. It draws scene geometry directly in a single renderer surface; it does not create a WinUI visual per primitive. Windows App SDK 1.8 provides the WinUI 3 host and is also MIT licensed.

`SpatialViewer.Rendering` contains only the backend-neutral `RenderFrame`, `RenderCommand`, preparation traversal, and `ISceneRenderer` contract. `SpatialViewer.Rendering.Windows` owns Win2D and the Windows-specific resource lifecycle. The renderer accepts selection state and redraws resources through `RecreateResources`; a production Direct3D device-loss callback can call the same contract without affecting Core.

Current Stage 1 geometry is intentionally basic: lines, polylines/polygons/path segments, rectangles, circles, arcs, ellipses, text placeholders, and image placeholders. It has no line types, hatch, CAD text layout, images, clipping, spatial index, or GPU batching yet.

A later 3D backend can implement a parallel `Scene3D` and renderer behind the same document/viewport direction. It must not introduce 3D or graphics API types into `Scene2D` or Core math.

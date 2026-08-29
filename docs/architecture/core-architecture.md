# Stage 1 core architecture

Spatial Viewer has a viewer-first, format-neutral core. `SpatialViewer.Core` has no WinUI, Windows SDK, renderer, importer, or format-SDK dependency.

```text
future CAD/GIS/IFC/3DM importer
             |
             v
 IDocument + Scene2D (Core) ---> RenderPreparation (Rendering) ---> ISceneRenderer
                                                                      |
                                                        Win2D/Direct2D Windows backend
                                                                      |
                                                           WinUI Debug Host only
```

`IDocument` carries the stable document identity, kind, display name, scene, layers, metadata, bounds, and diagnostics. `DocumentKind` reserves CAD, GIS, BIM, and Rhino without introducing source-format classes. Stage 1 supplies only `SyntheticDocument`.

`Scene2D` is composed of generic `SceneLayer`, `SceneNode`, `Geometry2D`, `Transform2D`, `SceneStyle`, metadata, and `ObjectId`. It intentionally has no DWG/DXF/IFC/Shapefile/Rhino object types. Future importers translate their parsed source data into these common primitives or future format-neutral extensions.

Nodes are immutable after construction; layers only expose visibility as runtime view state. Scene traversal flattens hierarchy and produces `SceneItem` values for bounds, hit testing, and rendering without a UI ViewModel per primitive. Resource ownership remains at the renderer boundary (`IDisposable`); future loading APIs should accept `CancellationToken` at importer/load-service level rather than tying Core objects to the UI thread.

The Debug Host is deliberately thin: it switches deterministic synthetic documents and delegates camera, hit test, visibility, and render preparation to Core/Rendering. It is not the product shell, navigation, or file-open experience.

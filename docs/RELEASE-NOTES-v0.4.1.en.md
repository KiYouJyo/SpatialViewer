# SpatialViewer v0.4.1

[简体中文](RELEASE-NOTES-v0.4.1.md) | [日本語](RELEASE-NOTES-v0.4.1.ja.md) | English

v0.4.1 is a hotfix for the blank Rhino viewport and UI hangs found during v0.4 field testing.

## Fixes
- 3DMCore 1.0.2 now generates display meshes from semantic Brep and Extrusion geometry when Rhino render meshes are not stored in the file.
- Prepared 3DM render-scene/NURBS tessellation runs off the WinUI UI thread to prevent the window from becoming unresponsive after loading.
- Win2D rendering now batches triangles per Mesh instance instead of creating a CanvasGeometry for every triangle, with bounded fill/wire/curve/point frame budgets.
- Black and near-black Rhino layer colors receive display-only contrast correction on dark canvases without changing source model colors.
- Complex Mesh selection highlighting uses bounded batched wire drawing instead of allocating huge edge hash sets.
- Mouse-wheel zoom works regardless of the active Select/Orbit/Pan tool.

## Preserved
This hotfix does not modify the CAD rendering path, CadCore, CAD toolbar behavior, title bar, tabs, navigation, Projects, Favorites, or existing theme/shell effects.

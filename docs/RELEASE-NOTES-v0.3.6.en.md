# SpatialViewer v0.3.6

## English

v0.3.6 is a real-drawing CAD fidelity release that formally integrates **SpatialViewer.CadCore v0.12.2**.

### CAD display fixes

- Restores `_Oblique` / `ARCHTICK` / `_ArchTick` architectural dimension end marks as slash ticks instead of generic V-shaped arrows.
- Fixes dimension text that could appear upside down for rotations equivalent to 180 degrees, keeping values such as `2250` readable.
- When a DWG contains a meaningful Paper Space layout with real paper entities and an active viewport, the viewer now displays the composed Layout Scene instead of always forcing Model Space.
- Fit, rendering, and HitTest now use the same current CAD Scene.
- Empty default Layout1 / Layout2 tabs do not trigger an automatic switch, preserving normal Model Space behavior.

### Kernel integration

- Bundled CAD kernel updated to **SpatialViewer.CadCore v0.12.2**.
- The bundled runtime directory now matches the actual kernel version at `Kernels/Bundled/0.12.2`, removing the stale `0.9.0` path marker.
- CadCore ABI remains `1.0.0.0`; host contract remains `SpatialViewer.CadHost 1.0.0`.
- The application keeps the existing `latest-stable` kernel policy without coupling the app product version to the CadCore product version.

### Boundary

This release does not infer unsupported Tianzheng proprietary semantics. `TCH_AXIS_LABEL`, `TCH_DRAWINGINDEX` / `TCH_INDEXPOINTER`, `TCH_DIMENSION2`, and modern `TCH_DIMENSION` remain under the existing raw / opaque / proxy and evidence-policy boundary.

### Acceptance focus

Please verify the same real DWG for the following:

1. architectural dimension ends render as slash ticks;
2. values such as `2250` remain upright and readable;
3. the full sheet border, title block, and Paper Space content appear correctly;
4. already-correct walls, doors/windows, furniture, colors, lineweights, and existing UI remain unchanged.

# UI Stage 1 verification

## Automated checks

- `dotnet build SpatialViewer.sln -c Debug`: passes with 0 warnings and 0 errors.
- `dotnet test SpatialViewer.sln -c Debug --no-build`: Core 9, Rendering 3, Integration 3, CAD 11, Presentation 8; 34 passed, 0 failed, 0 skipped.
- Formal App Debug and Release startup smoke: passes locally. The unpackaged app stores recents under the current user's local application-data directory rather than `ApplicationData.Current.LocalFolder`, which requires package identity.

## Manual acceptance scope

Run the formal `SpatialViewer.App` and use a Stage 2 `.dxf` fixture plus a valid `.dwg` fixture to verify open, fit, pan, wheel zoom, select, properties, layer visibility, tab switching, and close. Verify both application themes around the black CAD canvas. The DebugHost remains a separate regression host.

## Deferred CAD support

HATCH, DIMENSION, LEADER/MLEADER, full MTEXT formatting, SHX/BigFont fidelity, XREF, raster references, Paper Space/Layout, advanced linetype, and proxy/custom objects remain outside Stage 1. GIS, IFC, Rhino, measurement, and editing are not wired.

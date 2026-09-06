# SpatialViewer v0.4.2

[简体中文](RELEASE-NOTES-v0.4.2.md) | [日本語](RELEASE-NOTES-v0.4.2.ja.md) | English

v0.4.2 completes the formal CAD-kernel diagnostics integration and connects the previously prepared CAD compatibility-report exporter to the existing viewer UI.

## CAD compatibility report

- Adds **Export CAD compatibility report** at the bottom of the existing right-side Properties pane, preserving the current WinUI 3 controls, pane widths, toolbar geometry and canvas layout.
- Generates privacy-safe JSON directly from the loaded reader-independent `CadDocument`, including application/CadCore/adapter versions, allow-listed aggregate document metadata, structural custom-entity grouping, Proxy Graphics coverage, and the raw proxy-command structural diagnostics supplied by CadCore v0.12.10.
- Saves to `Desktop/SpatialViewer Diagnostics` by default with a LocalAppData fallback.
- Copies the exported path to the clipboard and reuses the existing InfoBar for success/failure feedback.

## Privacy boundary

The exporter uses an explicit allow-list. It does not serialize the drawing filename/path, entity or target handles, layer names, coordinates, raw property values, colors, text content, raw DWG/DXF bytes, or complete entity metadata.

Unknown raw proxy commands remain structural evidence only and are not promoted to Xiangyuan or any other proprietary semantics.

## Kernel

The v0.4.2 release pipeline continues to resolve and embed the latest stable compatible CadCore through the independent Host Contract. The corresponding formal kernel release is SpatialViewer.CadCore v0.12.10, with CLR ABI `1.0.0.0` unchanged.

## Preserved

Existing CAD rendering, CAD toolbar geometry, responsive left/right pane behavior, title bar, tabs, NavigationView, Projects/Favorites, and the Rhino 3DM rendering/responsiveness fixes from v0.4.1 remain unchanged.

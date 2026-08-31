# Spatial Viewer v0.2.5

This release fixes shell visual hierarchy, update-state lifetime, and the responsive CAD properties sidebar.

## Fixes

- Restored the native WinUI 3 / Fluent window hierarchy. The title bar and hamburger navigation area remain on Mica while page content uses `LayerFillColorDefaultBrush`, keeping chrome and content distinct in both light and dark themes.
- Removed the forced transparent `NavigationView` content plane. Normal, hover, pressed, and selected navigation item states are again owned by the native WinUI template.
- Moved update-check results into a process-lifetime shared session. After checking SpatialViewer or Cad Core updates, navigating away from About and returning preserves the previous result instead of resetting to Not checked.
- The Cad Core updater service is retained with the session state, so an available update can still be downloaded and staged after navigating away and back.
- The CAD properties area now remains a right-side `SplitView` sidebar at Large, Medium, and Small widths instead of becoming a Flyout over the drawing.
- Left and right sidebars share responsive widths of 300 / 240 / 220 DIP. On narrow windows the toolbar can scroll horizontally while the Layers and Properties controls remain reachable.

## Acceptance

- CI now rejects regressions that restore a transparent `NavigationViewContentBackground`, reintroduce the properties Flyout, or make About update state page-local again.
- The v0.2.4 Cad Core 0.3.0 → 0.3.1 real restart update, stable ABI, MSIX resolver isolation, version identity, and signing acceptance remain enforced.

## Versions

- SpatialViewer: 0.2.5
- MSIX: 0.2.5.0
- Bundled Cad Core: 0.3.0 fallback baseline
- Online Cad Core: independently updated from the latest compatible `SpatialViewer.CadCore` Release

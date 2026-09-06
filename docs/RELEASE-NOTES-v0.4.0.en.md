# SpatialViewer v0.4.0

[简体中文](RELEASE-NOTES-v0.4.0.md) | [日本語](RELEASE-NOTES-v0.4.0.ja.md) | English

v0.4.0 adds the Rhino 3DM viewing workflow while preserving the existing CAD viewer and WinUI 3 shell behavior.

## Rhino 3DM
- Integrates SpatialViewer.3DMCore 1.0.1.
- Opens .3dm directly with progressive loading, layer visibility, standard views, and Rhino named views.
- Supports perspective/orthographic cameras, orbit, pan, zoom, and fit.
- Supports Shaded, Shaded with Edges, and Wireframe display modes.
- Adds object selection, highlighting, Rhino property inspection, and instance-aware selection identities.
- Integrates recent files, session restore, external-file reload, and Windows .3dm file association.

## Interface consistency
The Rhino viewer follows the CAD viewer's 64-DIP toolbar, native AppBarButton/AppBarToggleButton controls, inline SplitViews, 300 / 240 / 220-DIP responsive pane widths, properties list, and status bar.

## Preserved
The CAD rendering path, CadCore runtime updater, title bar, tabs, navigation, themes, Projects, Favorites, and existing shell effects are not refactored by this release.

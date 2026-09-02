# SpatialViewer v0.3.3

[简体中文](RELEASE-NOTES-v0.3.3.md) | [日本語](RELEASE-NOTES-v0.3.3.ja.md) | English

## Tab interactions

v0.3.3 focuses on completing desktop title-bar tab interactions without changing the existing page rendering or overall visual design.

- New tabs now open with a Chrome-like horizontal expansion plus fade-in instead of the previous entrance motion that could appear to flash upward into place.
- The opening animation uses no Y-axis translation and does not scale text or icons, avoiding visible jumps or blur.
- Existing tab dimensions, corner radius, colors, selected/unselected states, and adjacent-tab repositioning are preserved.
- Middle-click now closes a tab directly, including background tabs without activating them first.
- Hovering a tab shows a preview card; CAD document tabs can render a lightweight drawing preview.
- CAD hover previews use an independent `Camera2D` and Win2D renderer, so preview fitting cannot change the live drawing zoom, pan, or selection state.

## Version and kernel

- Product version is now `0.3.3`, with MSIX package version `0.3.3.0`.
- Release packages continue to resolve and embed the newest stable `SpatialViewer.CadCore` version that has completed application integration.

## Unchanged surfaces

v0.3.3 does not change the existing title-bar geometry, hamburger navigation, NavigationView surfaces, page backgrounds, light/dark theme design, Projects/Favorites page layout, or the current CAD viewer rendering result.

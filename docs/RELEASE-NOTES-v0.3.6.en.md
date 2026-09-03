# SpatialViewer v0.3.6

[简体中文](RELEASE-NOTES-v0.3.6.md) | [日本語](RELEASE-NOTES-v0.3.6.ja.md) | English

## New application icon

v0.3.6 adopts the selected low-saturation planning/design mark as SpatialViewer's product icon. The four-panel drawing and central magnifier retain distinct CAD plan, GIS map, Rhino surface, and BIM massing cues, with transparent space outside the rounded icon.

- Updates the `Square150x150Logo`, `Square44x44Logo`, and `StoreLogo` package assets used by Windows Start and package surfaces.
- Adds small unplated shell assets for taskbar/pinned presentation and embeds a Windows executable icon as the window/unpackaged fallback.
- Shows the same product icon at the left of the custom title bar without changing its height, tab region, or window controls.
- Replaces the About-page placeholder mark with the formal product icon while preserving the existing information layout and responsive behavior.
- Refreshes the splash presentation with the new product mark on a low-saturation light background.

## Version

- Product version: `0.3.6`
- MSIX package version: `0.3.6.0`
- Cad Core integration and update behavior are unchanged.

## Preserved behavior

This is a branding-asset release. CAD import/rendering, tab interactions, NavigationView behavior, page surfaces, light/dark themes, Projects/Favorites data, and Cad Core update logic are unchanged.

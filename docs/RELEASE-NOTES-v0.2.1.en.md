[简体中文](RELEASE-NOTES-v0.2.1.md) | [日本語](RELEASE-NOTES-v0.2.1.ja.md) | English

# Spatial Viewer v0.2.1 Settings and About corrections

This release applies the hands-on review feedback for the v0.2.0 Settings and About pages without changing the CAD kernel version.

- Restores Windows Mica as the page backdrop by removing the opaque NavigationView content layer that produced a flat gray sheet.
- Uses neutral WinUI 3 Fluent Card / Control Fill resources for ordinary Settings and About cards instead of teal product-accent surfaces.
- Adopts the same responsive settings-row model as UrbanPlanToolbox: actions align on the right at wide widths and move below their descriptions on compact widths.
- Toggle switches and combo boxes share the same action column, so their right edges align in the wide layout.
- Removes the separate viewer-theme control. The viewer now always follows the application theme; only the drawing-background preference remains.
- Connects live Simplified Chinese, Japanese, and English switching, applies the change without restarting, and restores the saved language on the next launch.
- Reflows About metadata, update actions, kernel controls, and project cards across wide, medium, and compact layouts.
- Moves About above Settings in the hamburger-menu footer.
- Corrects the publisher display to `Jo Kiyō`.
- Shows the CAD kernel by its repository name, `SpatialViewer.CadCore`, with direct repository navigation.

## Current boundary

Complete CAD ACI color resolution and removal of arc faceting remain separate correctness work in `SpatialViewer.CadCore` and are not part of this UI correction release.

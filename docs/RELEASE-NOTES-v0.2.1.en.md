[简体中文](RELEASE-NOTES-v0.2.1.md) | [日本語](RELEASE-NOTES-v0.2.1.ja.md) | English

# Spatial Viewer v0.2.1 UI corrections and independent CadCore updates

This release applies the hands-on review feedback for the v0.2.0 Settings and About pages and introduces the first independent `SpatialViewer.CadCore` update path. The application keeps a bundled 0.2.0 kernel as a permanent fallback; the current independent CadCore 0.2.1 Release can be checked and downloaded in-app, then activated after the user manually restarts Spatial Viewer.

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
- Replaces the former repository-readability message with real CadCore version states: `Not checked → Checking → Up to date / Update available`; when a newer kernel exists, the action changes to Download update.
- Downloads the kernel package from GitHub Releases into the user's local app-data area without modifying the read-only MSIX installation. The updater verifies GitHub's SHA-256 digest, `cadcore-release.json`, x64 runtime, source repository, compatibility range, and the versions of all five required kernel assemblies.
- After download and verification, the UI reports that the update is ready and will apply after restart. No loaded assembly is hot-swapped. On the next normal launch, the staged kernel is loaded before the WinUI shell is created.
- The bundled kernel remains the fallback baseline. If a future Spatial Viewer build bundles a CadCore version equal to or newer than the external kernel, the newer bundled version automatically takes priority.

## CadCore 0.2.1

The independent `SpatialViewer.CadCore v0.2.1` Release is now published with `CadCore-v0.2.1-x64.zip` and a GitHub SHA-256 digest. It retains the current CadCore fixes for AutoCAD ACI 1–255 and TrueColor / ByLayer / ByBlock color semantics, together with adaptive arc tessellation using a default 0.25 px screen-error tolerance.

Spatial Viewer v0.2.1 intentionally keeps its bundled fallback kernel at 0.2.0 so the independent updater can exercise a genuine 0.2.0 → 0.2.1 discovery, download, and restart activation instead of merely shipping the same kernel again inside an application update.

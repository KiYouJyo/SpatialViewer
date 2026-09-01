# SpatialViewer v0.3.0 Release Notes

SpatialViewer v0.3 turns Projects and Favorites from placeholder navigation entries into working local organization features and ends the product-level Preview / Stable channel split.

## Added

- **Projects**: create a project, attach multiple drawings or spatial-data files, search projects, and reopen their files from project cards.
- **Import folder**: the sidebar action scans supported spatial-data extensions and creates a project named after the selected folder.
- **Favorites**: add frequently used files, search them, open them directly, and remove them with the star action.
- Projects and favorites are persisted in the user's local application-data directory; file contents are not uploaded.

## Update model

- The Preview / Stable product-channel split is removed. SpatialViewer now has a single product version stream.
- The app compares published GitHub Releases by semantic version, so every installed version older than the latest stable release can discover that update.
- The `v0.3` product version is `0.3.0`; the MSIX package version is `0.3.0.0`.

## UI constraints

- Projects and Favorites reuse the existing WinUI 3 theme resources, native button states, and card language.
- This release does not redesign the title bar, hamburger menu, NavigationView shell, or the already-corrected page-background hierarchy.

## Current format boundary

Projects and Favorites can organize CAD, GIS, IFC, and 3DM files. In-app viewing is still limited primarily to the currently integrated DWG / DXF CAD core. Additional formats will become viewable as their independent cores are integrated.

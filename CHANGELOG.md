# Changelog

## [0.3.3] - 2026-09-02

### Added

- Added mouse middle-button closing for title-bar tabs without activating background tabs first.
- Added native hover cards for tabs, including a live fitted CAD drawing preview for document tabs and a lightweight home-page preview.

### Changed

- Replaced the detached tab entrance transition with a Chrome-style horizontal width expansion plus short opacity fade; the animation uses no vertical translation or content scaling.
- Kept the existing title-bar geometry, tab sizing, colors, hamburger menu, NavigationView surfaces, page backgrounds, and selected/unselected visual treatment unchanged outside the new interaction states.
- Advanced the product and MSIX package identities to `0.3.3` / `0.3.3.0`.

## [0.3.0] - 2026-09-01

### Added

- Replaced the Projects placeholder with a persistent local project catalog supporting project creation, multi-file grouping, search, recent-open metadata, and reopen actions.
- Connected the Import Folder navigation action to recursive supported-format discovery and project creation.
- Replaced the Favorites placeholder with a persistent local favorites catalog supporting add, search, open, and remove actions.

### Changed

- Ended the product-level Preview / Stable distinction; SpatialViewer now follows one semantic-version product stream and every installed version below the latest published stable version can discover that update.
- Advanced the product and MSIX package identities to `0.3.0` / `0.3.0.0`.
- Kept the existing title bar, hamburger navigation, NavigationView surface hierarchy, theme resources, and fixed background behavior unchanged while adding the new pages.
- Improved title-bar tab responsiveness by caching document views, keeping open document viewers in a shared host during document-to-document switches, updating only the previous and next tab visuals, coalescing stale rapid-click activations, and avoiding repeated viewer NavigationView chrome reconfiguration.
- Changed the Simplified Chinese up-to-date application status from “已是最新预览版” to “已是最新版本”.

## [0.2.5] - 2026-08-31

### Fixed

- Restored the native Fluent shell hierarchy: title bar and hamburger navigation remain on Mica while the NavigationView content plane uses `LayerFillColorDefaultBrush` in light and dark themes.
- Returned normal, hover, pressed, and selected navigation-item treatment to the native WinUI NavigationView template instead of flattening chrome and content onto one transparent layer.
- Moved SpatialViewer and Cad Core update-check results into a process-lifetime About update session, matching UrbanPlanToolbox's shared update-view-model lifetime so navigation no longer resets the displayed result.
- Retained the Cad Core updater service with the session state so an `UpdateAvailable` result can still proceed to download/staging after leaving and returning to About.
- Replaced the medium/small CAD properties Flyout with the same inline SplitView sidebar model as the layers pane; responsive pane widths are now 300 / 240 / 220 DIP.
- Kept the CAD toolbar available at narrow widths with horizontal scrolling so pane toggles remain reachable without floating over the drawing.

## [0.2.4] - 2026-08-31

### Fixed

- Fixed the real v0.2.3 restart-update root cause: Cad Core product releases had been changing CLR `AssemblyVersion`, so a host compiled against the bundled kernel rejected the downloaded same-name assemblies with `0x80131040` manifest-definition mismatch.
- Decoupled Cad Core product version from binary ABI. SpatialViewer now reads the bundled/product version from `FileVersion`, validates external package `manifest.version` against file metadata, and treats `manifest.abiVersion` / `AssemblyVersion` as a separate compatibility contract.
- Pinned the v0.2.4 bundled Cad Core to a traceable 0.3.0 stable-ABI baseline (`AssemblyVersion 1.0.0.0`) and requires the online v0.3.1+ kernel to expose the same ABI before staging or activation.
- Kept staged Cad Core activation at module-initializer time so the compatible external implementation is preloaded into the default `AssemblyLoadContext` before generated WinUI/XAML or application code binds static Cad Core references.
- Replaced the old weak probe with a real static-binding regression: compile against bundled Cad Core 0.3.0, download the live newer release, restart a fresh process, construct `ACadSharpCadImporter`, and verify all five Cad Core assemblies have the newer FileVersion, the same ABI, and paths under the staged version directory.
- Activation diagnostics now record product version, ABI version, load path, pending state, and activation errors separately.

## [0.2.3] - 2026-08-31

### Fixed

- Made the CadCore action button remain enabled after an update is downloaded; it now changes to a restart-to-update action and uses Windows App SDK `AppInstance.Restart` to relaunch the application.
- The restarted process is expected to activate the already staged CadCore before XAML initialization; v0.2.4 corrects the activation timing defect that could still leave static references bound to the bundled version.
- Shortened the kernel display name to `Cad Core` and changed its update-source cell to `SpatialViewer.CadCore` so the UI distinguishes the component name from the independent repository name.

## [0.2.2] - 2026-08-31

### Fixed

- Hardened `SpatialViewer.CadCore` downloads with retry through the system proxy followed by a direct-connection fallback while retaining mandatory GitHub SHA-256 verification.
- Preserved structured CadCore update failure stages instead of collapsing every runtime failure into the generic “kernel update failed” status; detailed diagnostics are written to the local kernel update log and exposed as a tooltip.
- Added updater-specific acceptance gates so signed packages are not produced unless the live CadCore release can be downloaded and validated end to end.

## [0.2.1] - 2026-08-31

### Added

- Added real `SpatialViewer.CadCore` update checks that compare the active kernel version with the latest GitHub Release.
- Added SHA-256 verified CadCore package download, safe extraction, release-manifest/architecture/compatibility validation, and restart-only staging under the user's local app data.
- Added startup activation for a staged newer CadCore through the default assembly load context; the bundled kernel remains the fallback and automatically supersedes an older external kernel after a future app update.

### Fixed

- Restored the Window Mica backdrop on Settings and About instead of covering it with a flat NavigationView content layer.
- Replaced teal ordinary content cards with neutral WinUI 3 Fluent card/control surfaces matching UrbanPlanToolbox.
- Corrected medium/compact Settings and About reflow, including action-column collapse at narrow widths.
- Aligned toggle-switch right edges with combo-box right edges in the wide Settings layout.
- Connected live Simplified Chinese, Japanese, English, and system-language switching while preserving v0.2 stored language values.
- Removed the separate viewer-theme control; the viewer now follows the application theme.
- Moved About above Settings in navigation, corrected publisher text to `Jo Kiyō`, and labels the CAD kernel as `SpatialViewer.CadCore`.
- Replaced the former repository-reachability message with actionable CadCore states: check, up to date, update available, download, verify, and waiting for manual restart.

## [0.2.0] - 2026-08-31

### Added

- Implemented the Figma-designed Settings and About pages with native WinUI 3 materials and responsive nested card layouts.
- Added persisted application, session, file-monitoring, viewer-theme, and drawing-background preferences.
- Added GitHub Release checks for SpatialViewer and the independent SpatialViewer.CadCore, plus working repository, release, license, and privacy destinations.
- Added session restore and automatic reload of externally modified CAD files.
- Added trilingual v0.2.0 release notes and upgraded the signed acceptance pipeline to package version 0.2.0.0.

### Changed

- Fit-to-window on open is now owned by the viewer preference instead of being applied unconditionally by DocumentSession.
- External file reloads preserve the user's current camera state.

## Unreleased

- Added the foundational DXF/DWG import pipeline with isolated ACadSharp adapter, CAD document model, Scene2D translation, fixtures, diagnostics, and Debug Host file opening.
- Added `SpatialViewer.App`, a WinUI 3 Preview product shell with Figma semantic tokens, real title bar integration, recent files, tabbed CAD sessions, Win2D CAD workspace, layer/selection/properties presentation, diagnostics, and presentation regression tests.

## [0.1.0] - 2026-08-30

### Added

- Published the first installable Windows Preview through GitHub Releases.
- Added signed x64 MSIXBundle distribution, a lightweight one-click installer, SHA-256 checksums, trilingual release notes, and GitHub Pages homepage infrastructure.
- Added persisted theme selection and native Mica-compatible popup surfaces to the product shell.

本文件记录 Spatial Viewer 的用户可见变化与重要工程变化。

格式参考 [Keep a Changelog](https://keepachangelog.com/)，版本规划遵循语义化版本的基本原则；在首个可验证版本形成前，变化集中记录于 `Unreleased`。
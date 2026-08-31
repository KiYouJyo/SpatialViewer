# Changelog

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

## [Unreleased]

### Added

- 初始化项目中、日、英三语 README。
- 增加贡献指南、安全政策与行为准则。
- 增加路线图、支持说明、隐私说明和第三方声明。
- 增加 GitHub Issue 与 Pull Request 基础模板。

### Notes

- 项目仍处于早期开发阶段。
- README 与路线图中列出的格式属于计划覆盖范围，不应解读为当前版本已经完整支持。

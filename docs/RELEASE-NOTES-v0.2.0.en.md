[简体中文](RELEASE-NOTES-v0.2.0.md) | [日本語](RELEASE-NOTES-v0.2.0.ja.md) | English

# Spatial Viewer v0.2.0 Settings & About Preview

- Implements the Settings and About Spatial Viewer pages from the Figma design while preserving native WinUI 3 controls, materials, themes, and motion.
- Adds UrbanPlanToolbox-style responsive nested cards: desktop widths keep the layered surface hierarchy, while narrow windows reflow controls and cards instead of compressing them into unusable sizes.
- Persists app theme, language preference, session restore, recent files, external file-change monitoring, fit-on-open, viewer theme, and drawing background preferences.
- Fit-on-open is now controlled only by the viewer preference, and reloading an externally modified drawing preserves the current camera state.
- The About page performs real GitHub Release checks for SpatialViewer and the independent `SpatialViewer.CadCore`, with working repository, Releases, license, and privacy destinations.
- The CAD core remains integrated through its independent repository and pinned gitlink. GIS, IFC/BIM, and Rhino core entries explicitly remain not connected yet.
- Aligns application, assembly, MSIX, and release metadata on 0.2.0 / 0.2.0.0 and upgrades the signed acceptance pipeline.

## Current scope

This release does not alter the CAD rendering semantics accepted in the v0.1 line. The known CAD color-mapping and visibly segmented arc issues remain follow-up correctness work in CadCore.

## Installation

Use the signed MSIXBundle or offline acceptance package published for v0.2.0. The offline package includes the signed installer, public certificate, SHA-256 manifest, and acceptance record.

# Third-Party Notices

Spatial Viewer may use third-party open-source libraries, SDKs, codecs, format readers, rendering components, fonts, icons, sample data, or other external assets as development progresses.

## Current status

The repository uses these Stage 1 runtime dependencies:

- **Windows App SDK 1.8** — MIT License. Used only by the unpackaged WinUI 3 Debug Host.
- **Win2D 1.4.0** — MIT License. Direct2D/DirectWrite-backed 2D rendering surface used by `SpatialViewer.Rendering.Windows`.
- **ACadSharp 3.7.1** — MIT License. DXF/DWG decoder used only by `SpatialViewer.Formats.Cad.ACadSharp`; its types are copied into Spatial Viewer CAD records at the adapter boundary.

These packages are restored from NuGet and are not copied into this repository. This file remains the canonical place for human-readable third-party attribution that is not already fully represented by package metadata or license files.

Before adding a third-party dependency or asset, contributors must verify that:

1. its license is compatible with the way Spatial Viewer uses and distributes it;
2. required copyright and attribution notices are preserved;
3. redistribution rights cover any binaries, native libraries, fonts, icons, test data, or sample files that are committed or packaged;
4. commercial/proprietary format SDK terms are reviewed separately and are not assumed to be covered by the MIT License of this repository.

## Project license boundary

The [MIT License](LICENSE) applies to Spatial Viewer source code authored for this repository. It does not replace or modify licenses that apply to third-party components, file-format specifications, sample datasets, trademarks, or proprietary SDKs.

This notice will be expanded when concrete third-party components are introduced.

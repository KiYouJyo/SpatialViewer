[简体中文](README.md) | [日本語](README.ja.md) | English

# Spatial Viewer

A modern Windows viewer for CAD, GIS, BIM/IFC, and Rhino data.

[![Windows](https://img.shields.io/badge/Windows-WinUI%203-0078D4?logo=windows&logoColor=white)](https://github.com/KiYouJyo/SpatialViewer) [![Languages](https://img.shields.io/badge/Languages-%E4%B8%AD%E6%96%87%20%7C%20%E6%97%A5%E6%9C%AC%E8%AA%9E%20%7C%20English-6F42C1)](#languages) [![MIT License](https://img.shields.io/badge/License-MIT-D4A72C)](LICENSE)

> Spatial Viewer is in an early stage of development. The formats and capabilities below describe project goals and do not imply complete support in the current build.

## Purpose

Spatial Viewer aims to bring engineering drawings, geospatial data, and 3D models into one clean and consistent Windows viewing environment.

- **Viewing first** — fast opening, navigation, inspection, and lightweight information access rather than full authoring.
- **Consistent experience** — shared tabs, navigation, view controls, layers, and properties across data types where practical.
- **Native Windows UI** — built with WinUI 3 and designed around modern Windows interaction and theming.
- **Modular core** — format readers, parsers, and renderers are intended to remain as independent as practical.

## Planned coverage

| Domain | Main sources / formats | Goal |
| --- | --- | --- |
| CAD | AutoCAD / DWG / DXF | 2D drawing viewing, layers, and basic entity information |
| GIS | Common vector, raster, and map data | Spatial browsing, layer management, coordinates, and map display |
| BIM | Revit workflows / IFC | IFC model viewing, hierarchy, and properties |
| 3D | Rhino / 3DM | Geometry, layers, and basic object information |

The detailed support matrix will evolve with the core and be tracked in [ROADMAP.md](ROADMAP.md) and release notes.

## Scope

Spatial Viewer is a **viewer**, not a replacement for AutoCAD, full GIS desktop editors, Revit, or Rhino. The project prioritizes file opening, visual fidelity, navigation, layer/object information, and viewing performance before optional lightweight tools such as measurement and querying.

## Languages

Planned product names and UI languages:

- 简体中文: **图览**
- 日本語: **図覧**
- English: **Spatial Viewer**

Repository documentation will be kept in Chinese, Japanese, and English where practical.

## Development status

The repository is currently in the early 0.x development stage. `SpatialViewer.App` provides the WinUI 3 product shell, Home and Recent files, DWG/DXF viewing, multiple tabs, layers/selection/properties, plus the Projects and Favorites features integrated in v0.3. GIS, IFC, and Rhino viewing will be added progressively as their independent cores are integrated. See the [CAD compatibility matrix](docs/compatibility/cad.md) and [ROADMAP.md](ROADMAP.md) for current boundaries.

## Download and install

Current builds are distributed through a single product stream on [GitHub Releases](https://github.com/KiYouJyo/SpatialViewer/releases/latest); the product no longer distinguishes Preview and Stable channels. Prefer the one-click installer or signed MSIXBundle attached to the relevant Release and verify it with the checksum and certificate from the same Release.

Project homepage: https://kiyoujyo.github.io/SpatialViewer/

## Documentation

- [Roadmap](ROADMAP.md)
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.en.md)
- [Support](SUPPORT.md)
- [Security](SECURITY.en.md)
- [Code of Conduct](CODE_OF_CONDUCT.en.md)
- [Privacy](PRIVACY.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

## Contributing

Issues, format-compatibility reports, and feature proposals are welcome. Please read [CONTRIBUTING.en.md](CONTRIBUTING.en.md) before submitting code. Only share sample drawings, models, or spatial datasets that you have the right to disclose, and remove sensitive project, personal, or location information first.

## License

Spatial Viewer is licensed under the [MIT License](LICENSE).
# SpatialViewer v0.3.5

[简体中文](RELEASE-NOTES-v0.3.5.md) | [日本語](RELEASE-NOTES-v0.3.5.ja.md) | English

## CAD layer sorting

v0.3.5 fixes the CAD layer palette using file parse/encounter order and displays layer names in a CAD-style ascending initial-character order instead.

- Layers beginning with digits come first and sort from `0` to `9`.
- Layers beginning with Latin letters follow, sorting from `A` to `Z` without regard to case.
- Layers beginning with Han characters come next and use Simplified Chinese pinyin collation so the first character follows an `A` to `Z` reading order.
- Names beginning with other symbols, plus empty names, are placed after those three groups so they do not disrupt the common CAD layer sequence.
- Sorting changes only the presentation order of the layer palette. It does not copy or replace layer objects, so visibility toggles, selection-to-layer synchronization, colors, line types, and CAD rendering remain unchanged.
- The same ordering rule is reapplied after automatic file reloads.

## Version and kernel

- Product version is now `0.3.5`; the MSIX package version is `0.3.5.0`.
- Cad Core integration and update behavior are unchanged.

## Unchanged behavior

v0.3.5 does not change CAD parse or draw order, the title bar, tabs, hamburger navigation, NavigationView, page backgrounds, light/dark themes, or the existing CAD rendering result.

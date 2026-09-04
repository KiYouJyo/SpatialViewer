# SpatialViewer v0.3.8 Xiangyuan Kernel Acceptance

[简体中文](RELEASE-NOTES-v0.3.8.md) | [日本語](RELEASE-NOTES-v0.3.8.ja.md) | English

SpatialViewer v0.3.8 is an **on-device acceptance build for Xiangyuan Control Planning CAD drawings**. It does not redesign the UI, title bar, tabs, navigation, theme, or existing CAD presentation. The established kernel-integration contract resolves and embeds the latest stable SpatialViewer.CadCore in the release package.

## Acceptance scope

- The package is intended to embed **SpatialViewer.CadCore v0.12.7** with stable ABI `1.0.0.0` and `SpatialViewer.CadHost 1.0.0` compatibility.
- CadCore v0.12.7 includes Xiangyuan discovery, native-to-converted diff, multi-pair candidate consensus, parcel single-variable experiments, Proxy Graphics geometry evidence, reference/endpoint evidence, and strict whole-document A/B matching.
- Whole-document A/B pairs entities only by the same unique retained CAD handle plus exact class identity. It never falls back to coordinates, layers, text, or geometric similarity.
- Repeated unknown candidates remain globally `Unknown`; being observed in a Xiangyuan drawing does not promote them to native Xiangyuan semantics.
- Unverified parcel number, land-use, FAR, density, green rate, height, boundary, and control-indicator relationships remain fail-closed.

## Suggested on-device checks

1. Re-open ordinary DWG/DXF regression drawings and confirm no regressions in speed, color, text, curves, grids, or drawing frames.
2. Open native Xiangyuan control-planning drawings and verify that custom objects are preserved and usable Proxy Graphics remain visible.
3. Create a copy that changes only one parcel property or one boundary vertex and verify that subsequent A/B evidence isolates the structural change.
4. Produce an ordinary-CAD copy using Xiangyuan object-to-block/all-explode or result-output conversion and compare it with the native drawing.
5. Keep the original file and reproduction steps for any missing object, placement/color/text issue, or crash so it can become the next Reader/display regression.

This release does not claim complete v0.13 native Xiangyuan parcel semantics.

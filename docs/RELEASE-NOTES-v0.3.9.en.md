# SpatialViewer v0.3.9 Xiangyuan Land-use Fill Fix

[简体中文](RELEASE-NOTES-v0.3.9.md) | [日本語](RELEASE-NOTES-v0.3.9.ja.md) | English

SpatialViewer v0.3.9 is a focused fix for the **missing Xiangyuan land-use parcel fills** observed during v0.3.8 on-device acceptance.

- The release package embeds **SpatialViewer.CadCore v0.12.8**.
- ObjectARX `ProxyPolygon` effective color is now preserved as both stroke and fill.
- Safe ACI / TrueColor primitive overrides propagate to the fill.
- Polyline, closed LwPolyline, Circle, Arc and Mesh/Shell edge fallback remain unfilled.

No UI, title-bar, tab, theme, or navigation changes are included. This restores generic proxy-polygon display semantics and does not infer Xiangyuan parcel semantics.

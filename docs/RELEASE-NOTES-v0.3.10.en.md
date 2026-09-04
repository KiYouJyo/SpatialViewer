# SpatialViewer v0.3.10 Xiangyuan Surface Fill Fix

[简体中文](RELEASE-NOTES-v0.3.10.md) | [日本語](RELEASE-NOTES-v0.3.10.ja.md) | English

SpatialViewer v0.3.10 is the second focused fix for Xiangyuan drawings that still rendered land-use areas as outlines only in v0.3.9.

- The package embeds **SpatialViewer.CadCore v0.12.9**.
- ObjectARX `ProxySubentFillon` state is now retained.
- Strictly planar, structurally valid `ProxyMesh` / `ProxyShell` primitives preserve face geometry plus face color/visibility instead of being collapsed to edges only.
- Invalid or non-planar face evidence falls back to edge-only without guessing fill geometry.
- Explicit FillNever suppresses ProxyPolygon fill; polylines remain non-fillable even when closed.

No UI or interaction changes are included. This restores generic ObjectARX display fidelity and does not infer proprietary Xiangyuan parcel semantics.

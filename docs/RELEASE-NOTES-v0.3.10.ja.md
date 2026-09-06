# SpatialViewer v0.3.10 湘源用途面塗り修正

[简体中文](RELEASE-NOTES-v0.3.10.md) | 日本語 | [English](RELEASE-NOTES-v0.3.10.en.md)

SpatialViewer v0.3.10 は v0.3.9 の実機検証で「湘源の用途ブロックが依然として輪郭のみ」と確認された問題の第2回修正版です。

- **SpatialViewer.CadCore v0.12.9** を組み込みます。
- ObjectARX `ProxySubentFillon` の FillAlways / FillNever 状態を保持します。
- 厳密に平面で構造が正しい `ProxyMesh` / `ProxyShell` は edge-only に落とさず、face geometry と face color / visibility を保持して塗りつぶします。
- 不正・非平面・不整合な FaceTraits は推測せず edge-only にフォールバックします。
- FillNever は ProxyPolygon の fill を抑止し、Polyline は閉じていても塗りつぶしません。

UI や既存操作は変更しません。本版は generic ObjectARX display-fidelity 修正であり、湘源 parcel semantic の推測ではありません。

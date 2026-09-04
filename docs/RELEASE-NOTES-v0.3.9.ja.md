# SpatialViewer v0.3.9 湘源用途色面修正

[简体中文](RELEASE-NOTES-v0.3.9.md) | 日本語 | [English](RELEASE-NOTES-v0.3.9.en.md)

SpatialViewer v0.3.9 は v0.3.8 の実機検証で確認された **湘源控規の用途色面が塗りつぶされない問題**の修正版です。

- **SpatialViewer.CadCore v0.12.8** を組み込みます。
- ObjectARX `ProxyPolygon` の effective color を stroke だけでなく fill にも保持します。
- ACI / TrueColor primitive override も fill に反映します。
- Polyline / closed LwPolyline / Circle / Arc / Mesh-Shell edge は従来どおり塗りつぶしません。

UI、タイトルバー、タブ、テーマ、ナビゲーションは変更しません。これは generic Proxy Polygon 表示の修正であり、湘源 parcel semantic の推測ではありません。

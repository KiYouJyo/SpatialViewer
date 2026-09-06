# SpatialViewer v0.4.0

[简体中文](RELEASE-NOTES-v0.4.0.md) | 日本語 | [English](RELEASE-NOTES-v0.4.0.en.md)

v0.4.0 では Rhino 3DM ビューアーを追加し、既存の CAD ビューアーと WinUI 3 シェルの挙動を維持します。

## Rhino 3DM
- SpatialViewer.3DMCore 1.0.1 を統合。
- .3dm の直接読み込み、段階的読み込み、レイヤー表示、標準ビュー、Rhino の名前付きビューに対応。
- 透視/平行投影、回転、パン、ズーム、全体表示に対応。
- シェーディング、エッジ付きシェーディング、ワイヤーフレームに対応。
- オブジェクト選択、ハイライト、Rhino プロパティ表示、インスタンスを区別する選択 ID に対応。
- 最近使ったファイル、前回セッション復元、外部変更時の再読み込み、Windows の .3dm 関連付けに対応。

## UI の統一
Rhino ビューアーは CAD ビューアーと同じ 64 DIP ツールバー、AppBarButton / AppBarToggleButton、Inline SplitView、300 / 240 / 220 DIP のレスポンシブなサイドバー、プロパティ一覧、ステータスバーを使用します。

## 維持される機能
CAD 描画経路、CadCore ランタイム更新、タイトルバー、タブ、ナビゲーション、テーマ、プロジェクト、お気に入りなど既存の挙動は変更しません。

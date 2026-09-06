# SpatialViewer v0.4.2

[简体中文](RELEASE-NOTES-v0.4.2.md) | 日本語 | [English](RELEASE-NOTES-v0.4.2.en.md)

v0.4.2 では CAD カーネル診断チェーンを正式リリースに接続し、既に準備していた CAD 互換性レポート出力を既存のビューア UI に統合しました。

## CAD 互換性レポート

- CAD ページ右側の「プロパティ」サイドバー下部に「CAD 互換性レポートを出力」を追加しました。既存の WinUI 3 コントロール、サイドバー幅、レイアウト規則を維持し、ツールバーやキャンバス配置は変更しません。
- 現在読み込み済みの reader-independent `CadDocument` から JSON を生成し、アプリ/CadCore/adapter のバージョン、安全な集計 metadata、custom entity の構造分類、Proxy Graphics coverage、v0.12.10 の raw proxy-command structural diagnostics を含めます。
- 既定ではデスクトップの `SpatialViewer Diagnostics` に保存し、利用できない場合は LocalAppData にフォールバックします。
- 成功時は出力パスをクリップボードへコピーし、既存の InfoBar で結果を通知します。

## プライバシー境界

明示的な allow-list を使用し、図面ファイル名/パス、entity/target handle、layer 名、座標、raw property value、色、テキスト、raw DWG/DXF bytes、完全な entity metadata は出力しません。

unknown proxy command は構造証拠としてのみ扱い、湘源などの proprietary semantic を推測しません。

## カーネル

v0.4.2 の正式リリースは独立 Host Contract に基づいて最新の安定 CadCore を解決・同梱します。対応する正式カーネルは SpatialViewer.CadCore v0.12.10、CLR ABI は引き続き `1.0.0.0` です。

## 維持事項

既存の CAD レンダリング、CAD ツールバー形状、左右サイドバーのレスポンシブ規則、タイトルバー、タブ、NavigationView、Projects/Favorites、および v0.4.1 の Rhino 3DM 表示・応答性修正は変更しません。

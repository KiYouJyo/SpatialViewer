# Spatial Viewer v0.2.5

本リリースでは、シェルの視覚階層、更新状態のライフサイクル、CAD のレスポンシブなプロパティサイドバーを修正します。

## 修正

- WinUI 3 / Fluent 本来のウィンドウ階層を復元しました。タイトルバーとハンバーガーナビゲーション領域は Mica を維持し、コンテンツ領域は `LayerFillColorDefaultBrush` を使用します。ライト / ダークの両テーマで chrome とページ内容を明確に区別できます。
- `NavigationView` のコンテンツ背景を強制的に透明化しません。通常、ホバー、押下、選択の各状態は WinUI 標準テンプレートに戻しました。
- 更新確認結果をプロセス単位の共有 session に移しました。SpatialViewer または Cad Core の確認後に別ページへ移動して戻っても、最後の確認結果が保持されます。
- Cad Core updater service 自体も session に保持するため、更新検出後にページを移動して戻ってもダウンロードとステージングを継続できます。
- CAD のプロパティ領域は Large / Medium / Small の全サイズで右側 `SplitView` サイドバーを使用し、中小サイズで図面上に Flyout として重なることがなくなりました。
- 左右サイドバーは同じレスポンシブ幅（300 / 240 / 220 DIP）を使用します。狭いウィンドウではツールバーを横スクロールでき、レイヤーとプロパティの切り替えも維持されます。

## 受け入れテスト

- 透明な `NavigationViewContentBackground` の再導入、プロパティ Flyout の再導入、About ページの非 session 更新状態を CI で禁止します。
- v0.2.4 で確立した Cad Core 0.3.0 → 0.3.1 の実リスタート更新、安定 ABI、MSIX resolver 分離、バージョン整合性、署名テストを継続します。

## バージョン

- SpatialViewer: 0.2.5
- MSIX: 0.2.5.0
- バンドル Cad Core: 0.3.0 フォールバック
- オンライン Cad Core: `SpatialViewer.CadCore` の最新互換 Release から独立更新

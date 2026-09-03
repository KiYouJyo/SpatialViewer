# SpatialViewer v0.3.6

## 日本語

v0.3.6 は、実際の建築 CAD 図面との比較で確認された表示差分を修正し、**SpatialViewer.CadCore v0.12.2** を正式統合するリリースです。

### CAD 表示修正

- 建築寸法スタイルの `_Oblique` / `ARCHTICK` / `_ArchTick` を斜線 tick として描画し、generic V-arrow への退化を修正しました。
- 180° 相当の回転で寸法文字が上下逆になる問題を修正し、`2250` などの値を読みやすい向きに保ちます。
- DWG に実際の Paper Space 要素と active viewport を持つ Layout が存在する場合、Model Space 固定ではなく合成 Layout Scene を表示します。
- Fit、描画、HitTest が同一の現在 CAD Scene を使用するよう統一しました。
- 空の既定 Layout1 / Layout2 では自動切替せず、通常の Model Space 図面の従来動作を維持します。

### カーネル統合

- バンドル CAD カーネルを **SpatialViewer.CadCore v0.12.2** に更新しました。
- bundled kernel の実行時ディレクトリを `Kernels/Bundled/0.12.2` に同期し、旧 `0.9.0` 表示との不整合を解消しました。
- CadCore ABI は `1.0.0.0`、host contract は `SpatialViewer.CadHost 1.0.0` のままです。
- アプリと CadCore の product version を直接結合せず、既存の `latest-stable` 統合方針を維持します。

### 境界

本リリースは未検証の天正 proprietary semantic を推測で追加しません。`TCH_AXIS_LABEL`、`TCH_DRAWINGINDEX` / `TCH_INDEXPOINTER`、`TCH_DIMENSION2`、modern `TCH_DIMENSION` は従来どおり raw / opaque / proxy と evidence policy の境界を維持します。

### 受入確認ポイント

同じ実 DWG で次を重点的に確認してください。

1. 建築寸法端点が斜線 tick に戻っていること。
2. `2250` などの寸法文字が正しい向きで読めること。
3. 図枠、タイトル欄、Paper Space 内容が完全に表示されること。
4. 既に正常な壁、建具、家具、色、線幅、既存 UI に回帰がないこと。

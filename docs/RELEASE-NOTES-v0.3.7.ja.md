# SpatialViewer v0.3.7 起動画面と CadCore v0.12.3 統合

SpatialViewer v0.3.7 は UrbanPlanToolbox と PageArc と同じ2段階起動方式を維持すると同時に、CAD 実図面修正の検証で見つかった重要な問題を修正します。これまでテストしていた Viewer は、実際には CadCore v0.12.3 のコードを実行していませんでした。

## 起動画面

- Stage 1 として MSIX `uap:SplashScreen` を残し、100%、125%、150%、200%、400% の DPI リソースと `uap5:Optional="true"` を維持します。
- Stage 2 では実際の WinUI メインウィンドウ内に透明な `StartupOverlay` を使用し、ウィンドウ自身の `MicaBackdrop` を起動直後から表示します。
- 実際の Shell は Logo の背後で初期化し、起動画面終了までは操作を受け付けません。
- Logo の実描画後に最短表示時間を計測し、高速起動時でも約 500 ms 表示した後、約 200 ms の EaseOut でフェードアウトします。
- 1 秒の Logo フォールバックと 5 秒の fail-open watchdog を維持します。

## CadCore 配布経路の修正

CadCore v0.12.3 の単体テストが通っていても Viewer の表示不具合が直ったことにはなりませんでした。実際の製品側は古いカーネルを使用していたためです。

- 公開済み SpatialViewer v0.3.6 は CadCore v0.12.2 を同梱していました。
- v0.3.7 のソースプロジェクトは `CadCoreBundledVersion=0.9.0` のままでした。
- CadCore submodule も v0.9.0 を指したままでした。

v0.3.7 ではソース submodule と source-build の bundled version を **CadCore v0.12.3 / commit `2f150fbdcf380fba6f60df7f8a41361322afdd8f`** に固定します。Acceptance には次の2つの強制 gate を追加します。

1. 宣言された version または gitlink が v0.12.3 でなければ source build を失敗させます。
2. 最終 MSIXBundle を展開し、`Kernels/Bundled/0.12.3` 内の5つの assembly が公開済み CadCore v0.12.3 release payload と SHA-256 で完全一致することを確認します。

これにより「カーネルリポジトリだけ修正したが Viewer は旧カーネルを実行している」という偽の修正完了を防ぎます。

## 実図面 Acceptance の境界

長い直角線、寸法文字の anchor / color / architectural tick、旧式 CJK SHX fallback に対する v0.12.3 の変更は、v0.3.7 で初めて実際にアプリへ同梱されます。ただし問題を再現する元 DWG がない状態では、これらは **candidate fix** であり、視覚的な修正完了とは扱いません。

最終 Acceptance は元図面を AutoCAD と SpatialViewer で直接比較し、3つの差異が実際に消えた時点でのみ完了とします。

## 変更しない範囲

- 既存のタイトルバー、ハンバーガーメニュー、NavigationView、ページ背景、タブ、既存インタラクションのレイアウトは本統合で再設計しません。
- 第2の独立 WinUI ウィンドウ、文字、ボタン、疑似プログレスバーは追加しません。

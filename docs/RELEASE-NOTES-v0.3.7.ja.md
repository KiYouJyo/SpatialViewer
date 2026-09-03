# SpatialViewer v0.3.7 起動画面とテーマ修正

[简体中文](RELEASE-NOTES-v0.3.7.md) | 日本語 | [English](RELEASE-NOTES-v0.3.7.en.md)

SpatialViewer v0.3.7 は、UrbanPlanToolbox と PageArc の現行実装と同じ2段階の起動方式へ修正しました。Windows ネイティブの SplashScreen が最初のプロセス起動を覆い、その後は実際の WinUI メインウィンドウ上で Mica を透過表示する起動レイヤーへ引き継ぎ、Shell の準備完了後に滑らかに消えます。正式リリースには、起動ラッパー導入後に判明したライトテーマ伝播の回帰修正も含まれます。

## 主な変更

- Stage 1 として MSIX `uap:SplashScreen` を残し、100%、125%、150%、200%、400% の DPI リソースと `uap5:Optional="true"` を維持します。
- Stage 2 として実際のメインウィンドウ内に透明な `StartupOverlay` を追加し、固定 `#202020` 画像から完成済み UI へ直接切り替えるのではなく、ウィンドウ自身の `MicaBackdrop` を起動直後から見せます。
- 実際の Shell は Logo の背後で初期化し、起動画面終了までは操作を受け付けません。
- Logo が実際に1フレーム描画された後に最短表示時間を計測し、高速起動時でも約 500 ms は完全な Logo を表示します。初期化が長い場合は追加の固定待機を入れず、そのまま Shell 完了を待ちます。
- Shell・Logo・最短表示時間がそろった後、完成した UI を先に表示し、約 200 ms の EaseOut で起動レイヤーをフェードアウトします。
- 1 秒の Logo デコード・フォールバックと 5 秒の startup watchdog を追加し、起動レイヤー自体が停止原因にならないよう fail-open します。
- ビルド契約を「ネイティブ Stage 1 + Mica Stage 2」に変更し、既存タイトルバーのジオメトリも回帰防止対象にします。
- 起動ラッパーで分断されていたテーマ所有権を修正し、`WindowRoot` を唯一のテーマルート、`RootGrid` を継承側へ戻して Light / Dark / System をウィンドウ全体で一貫して適用します。

## 変更しない範囲

- ライト/ダーク配色そのものは再設計せず、タイトルバー、ハンバーガーメニュー、NavigationView、ページレイアウト、タブ、CAD 描画、Cad Core の挙動は変更しません。
- 第2の独立 WinUI ウィンドウ、文字、ボタン、疑似プログレスバーは追加しません。

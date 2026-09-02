# SpatialViewer v0.3.1

## カーネル互換性修正

v0.3.1 では、独立更新可能な CAD カーネルの互換性モデルを再設計し、SpatialViewer 本体の製品バージョンがカーネル側で宣言された 0.x.x 系列と一致しなければならないという誤った制約を廃止します。

- カーネル更新可否を SpatialViewer 0.2.x / 0.3.x などの製品バージョンで判定しません。
- 独立した Host Contract `SpatialViewer.CadHost 1.0.0` を導入します。
- CLR ABI `1.0.0.0` は引き続きアセンブリのバインド互換性を担当し、Host Contract は宿主機能の互換性を独立して担当します。
- 「更新を確認」では、まず小さな独立 `cadcore-release.json` manifest を取得し、schema、ABI、Host Contract、version、runtime、source repository を事前検証します。事前検証に通った場合だけインストール可能な更新として表示します。
- 完全な ZIP の取得後も、アーカイブ内 manifest、アセンブリ ABI、FileVersion、事前検証 manifest との一致を再検証します。
- Release packaging と CI は同じ ABI + Host Contract ルールを使用し、SpatialViewer の minor 製品バージョンをハードコードしません。
- 安全なフォールバックは維持され、互換性のない、または破損した外部カーネルが内蔵安定カーネルを置き換えることはありません。

v0.3.1 では Projects、Favorites、タイトルバー、ナビゲーション、テーマ、既存のビューア操作デザインを変更しません。

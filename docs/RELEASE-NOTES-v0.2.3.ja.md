[简体中文](RELEASE-NOTES-v0.2.3.md) | 日本語 | [English](RELEASE-NOTES-v0.2.3.en.md)

# Spatial Viewer v0.2.3

この更新では、CadCore のダウンロード完了後に行う最後の更新操作を修正します。

## CadCore の再起動更新

- CadCore のダウンロードと検証が完了した後も、操作ボタンは無効化されません。
- ボタンは「再起動して更新」に変わり、Windows App SDK の `AppInstance.Restart` を使用して SpatialViewer を再起動します。
- 再起動したプロセスは XAML 初期化前にステージ済み CadCore を読み込み、ダウンロード済みの新しいカーネルを有効化します。実行中のホットスワップは行いません。
- Windows がアプリを再起動できない場合は再試行可能な状態を維持し、具体的な再起動失敗理由を記録します。

## 更新管理の表示修正

- カーネル名の列を `SpatialViewer.CadCore` から `CadCore` に簡略化しました。
- 更新元の列を `GitHub Releases` から `SpatialViewer.CadCore` に変更し、独立した CadCore リポジトリから更新されることを明示します。

## 検証

v0.2.3 の署名済み受け入れパッケージは、3 言語リソース、再起動更新 UI 契約、Debug/Release ビルド、テスト、起動 smoke、CadCore Release 検証、実ダウンロードとステージング、MSIX 生成、Authenticode 署名がすべて成功した場合のみ生成されます。

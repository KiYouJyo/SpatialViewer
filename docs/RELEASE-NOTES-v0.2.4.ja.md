# Spatial Viewer v0.2.4

本バージョンでは、v0.2.3 で残っていた **Cad Core をダウンロードして再起動しても旧カーネルにバインドされる問題**を、起動アーキテクチャから修正しました。

## 修正内容

- Cad Core のアクティブ化を `App()` コンストラクターからアセンブリの `ModuleInitializer` へ前倒しし、WinUI/XAML 生成コードや `MainWindow` の静的 Cad Core 参照が解決される前に新しいカーネルを選択します。
- 内蔵 Cad Core は安全なフォールバックとして保持し、manifest、アーキテクチャ、互換性、アセンブリバージョン検証を通過した外部カーネルのみを有効化します。
- 「再起動して更新」は引き続き Windows App SDK の `AppInstance.Restart` を使用しますが、新しいプロセスでは WinUI 初期化前に `pending.json` を処理し、新しい Cad Core にバインドします。
- 受け入れテスト用 probe を実アプリと同様に 5 つの Cad Core プロジェクトへ静的参照させ、モジュール初期化後に `ACadSharpCadImporter` を実際に生成して、新しいアセンブリへ静的型参照がバインドされることを確認します。
- 5 つの Cad Core アセンブリについて、バージョンだけでなくロード元が MSIX 内蔵ディレクトリではなくステージ済みバージョンディレクトリであることも検証します。

## バージョン

- SpatialViewer: 0.2.4
- MSIX: 0.2.4.0
- 内蔵 Cad Core: フォールバック基準として維持
- オンライン Cad Core: `SpatialViewer.CadCore` の最新互換 Release から独立更新

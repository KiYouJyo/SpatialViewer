[简体中文](RELEASE-NOTES-v0.2.4.md) | 日本語 | [English](RELEASE-NOTES-v0.2.4.en.md)

# Spatial Viewer v0.2.4

本更新では、v0.2.3 で Cad Core をダウンロードして再起動しても実際のアプリが旧カーネルへバインドされたままになる問題を、起動レイヤーで修正します。

## Cad Core 起動時プリロード

- Cad Core のアクティベーションを WinUI `App` コンストラクターから CLR `ModuleInitializer` へ前倒ししました。
- ステージ済みの新しいカーネルは、`Microsoft.UI.Xaml.Application` の構築、XAML 型システム初期化、および製品コードが Cad Core 型へ初めてアクセスする前にロードされます。
- 内蔵 Cad Core は安全なフォールバックとして維持し、バージョン・マニフェスト・アセンブリ検証を通過した更新だけを active にします。
- 「再起動して更新」は引き続き Windows App SDK `AppInstance.Restart` を使用し、新しいプロセスでは WinUI 起動前にカーネルをバインドします。

## 実機問題に対応した受け入れテスト

- 静的バインド起動テストを追加しました。テストプロセス自体は旧 Cad Core をコンパイル時参照し、オンライン最新版を pending として配置します。
- `Main` 実行前に pending → active が完了し、`Main` から実際の `ACadSharpCadImporter` 型へ直接アクセスした際、そのアセンブリがオンライン最新版であることを必須条件にします。
- テスト後は製品本来の Cad Core gitlink を復元するため、最終 MSIX の内蔵カーネルがテスト用にダウングレードされることはありません。

## パッケージバージョン

- Spatial Viewer: 0.2.4
- MSIX: 0.2.4.0
- Architecture: x64

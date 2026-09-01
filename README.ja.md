[简体中文](README.md) | 日本語 | [English](README.en.md)

# Spatial Viewer · 図覧

CAD、GIS、BIM/IFC、Rhino データを扱うモダンな Windows ビューアーです。

[![Windows](https://img.shields.io/badge/Windows-WinUI%203-0078D4?logo=windows&logoColor=white)](https://github.com/KiYouJyo/SpatialViewer) [![Languages](https://img.shields.io/badge/Languages-%E4%B8%AD%E6%96%87%20%7C%20%E6%97%A5%E6%9C%AC%E8%AA%9E%20%7C%20English-6F42C1)](#言語) [![MIT License](https://img.shields.io/badge/License-MIT-D4A72C)](LICENSE)

> Spatial Viewer は現在、開発初期段階です。以下の形式や機能はプロジェクトの目標であり、現行ビルドでの完全対応を示すものではありません。

## プロジェクトの目的

Spatial Viewer は、設計図面、地理空間データ、3D モデルを、統一されたシンプルな Windows 環境で閲覧できるようにすることを目指しています。

- **閲覧を優先** — 専門ソフトを置き換えるのではなく、素早い表示、移動、確認、軽量な情報参照に重点を置きます。
- **一貫した操作** — 可能な範囲で、タブ、ナビゲーション、ビュー操作、レイヤー、プロパティの体験を統一します。
- **Windows ネイティブ** — WinUI 3 を採用し、Windows のテーマと操作体系に沿った UI を構築します。
- **モジュール化されたコア** — 各形式の読み込み、解析、描画を可能な限り独立した構成にします。

## 対応予定

| 分野 | 主なソース / 形式 | 目標 |
| --- | --- | --- |
| CAD | AutoCAD / DWG / DXF | 2D 図面表示、レイヤー、基本エンティティ情報 |
| GIS | 一般的なベクター、ラスター、地図データ | 空間データ表示、レイヤー管理、座標・地図表示 |
| BIM | Revit ワークフロー / IFC | IFC モデル表示、階層・プロパティ参照 |
| 3D | Rhino / 3DM | ジオメトリ、レイヤー、基本オブジェクト情報 |

詳細な対応範囲はコア開発に合わせて確定し、[ROADMAP.md](ROADMAP.md) とリリースノートで更新します。

## 対象範囲

Spatial Viewer は**ビューアー**であり、AutoCAD、フル機能の GIS デスクトップ編集ソフト、Revit、Rhino の代替を目的としていません。まずファイル表示、再現性、ナビゲーション、レイヤー／オブジェクト情報、表示性能を優先し、その後必要に応じて計測や照会などの軽量ツールを追加します。

## 言語

予定している製品名と UI 言語：

- 简体中文：**图览**
- 日本語：**図覧**
- English：**Spatial Viewer**

リポジトリの主要文書も、可能な範囲で中国語・日本語・英語を同期します。

## 開発状況

リポジトリは現在 0.x の初期開発段階です。`SpatialViewer.App` には WinUI 3 の製品シェル、ホーム／最近使ったファイル、DWG/DXF 表示、複数タブ、レイヤー／選択／プロパティ、および v0.3 で接続されたプロジェクト／お気に入り機能があります。GIS、IFC、Rhino の実表示機能は独立コアの統合に合わせて順次追加します。詳細は [CAD 互換性マトリクス](docs/compatibility/cad.md) と [ROADMAP.md](ROADMAP.md) を参照してください。

## ダウンロードとインストール

現在のバージョンは [GitHub Releases](https://github.com/KiYouJyo/SpatialViewer/releases/latest) の単一製品チャンネルから配布し、Preview / Stable の製品チャンネルは区別しません。各 Release に含まれるワンクリックインストーラーまたは署名済み MSIXBundle を使用し、同じ Release のチェックサムと証明書で検証してください。

プロジェクトホームページ: https://kiyoujyo.github.io/SpatialViewer/

## ドキュメント

- [ロードマップ](ROADMAP.md)
- [変更履歴](CHANGELOG.md)
- [コントリビューションガイド](CONTRIBUTING.ja.md)
- [サポート](SUPPORT.md)
- [セキュリティポリシー](SECURITY.ja.md)
- [行動規範](CODE_OF_CONDUCT.ja.md)
- [プライバシー](PRIVACY.md)
- [サードパーティー通知](THIRD-PARTY-NOTICES.md)

## コントリビューション

Issue による不具合報告、形式互換性の報告、機能提案を歓迎します。コードを提出する前に [CONTRIBUTING.ja.md](CONTRIBUTING.ja.md) を確認してください。サンプル図面、モデル、空間データは公開権限があるものだけを使用し、個人情報、プロジェクト情報、機微な位置情報を除去してください。

## License

本プロジェクトは [MIT License](LICENSE) の下で公開します。
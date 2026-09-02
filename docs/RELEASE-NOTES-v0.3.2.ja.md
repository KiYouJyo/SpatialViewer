# SpatialViewer v0.3.2

[简体中文](RELEASE-NOTES-v0.3.2.md) | 日本語 | [English](RELEASE-NOTES-v0.3.2.en.md)

## アプリ内アップデート

v0.3.2 では、SpatialViewer 本体の更新処理を UrbanPlanToolbox と同じ検証済み MSIX 更新方式へ変更しました。バージョン確認後にブラウザーへ移動するだけの方式ではありません。

- 「SpatialViewer について」の本体更新状態はプロセス単位で共有され、ページを離れて戻っても確認・ダウンロード状態を保持します。
- GitHub Releases を確認し、現在のインストール版より新しい正式版だけを更新候補にします。
- Release には一意の `SpatialViewer_<version>.0_x64.msixbundle` と `SHA256SUMS.txt` が必要です。
- ダウンロード後、GitHub asset の SHA-256、`SHA256SUMS.txt`、WinTrust/MSIX 署名、発行証明書の Subject と Thumbprint を順に検証します。
- 信頼する発行者は `CN=AppPublisher`、証明書 Thumbprint は `BD85AD77A651C86CA01A480C8E9BC64952993F98` です。
- 検証完了後は「インストール待ち」状態となり、ユーザーの再確認後に Windows `PackageManager` が `ForceApplicationShutdown` で新しい MSIXBundle を展開します。アプリ再起動登録により更新後の復帰を行います。
- ダウンロード、検証、インストール、失敗、再試行の状態は既存の「更新管理」領域に表示し、独自 Launcher は追加しません。

## バージョン表示の修正

- 「表示バージョン」「内部バージョン」「現在のバージョン」を XAML に固定値で記述しないようにしました。
- 表示は `AppVersionProvider` に統一し、パッケージ版では `Package.Current.Id.Version` を優先します。
- v0.3.2 の製品バージョンは `0.3.2`、MSIX パッケージバージョンは `0.3.2.0` です。

## 変更しない範囲

v0.3.2 はタイトルバー、ハンバーガーメニュー、ナビゲーション構造、プロジェクト／お気に入り画面、ライト／ダークテーマ設計、CadCore の独立更新機構を変更しません。

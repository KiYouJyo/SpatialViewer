# SpatialViewer v0.3.8 湘源控規カーネル実機検証

[简体中文](RELEASE-NOTES-v0.3.8.md) | 日本語 | [English](RELEASE-NOTES-v0.3.8.en.md)

SpatialViewer v0.3.8 は **湘源控規 CAD の実図面検証**を目的としたカーネル受入れ版です。UI、タイトルバー、タブ、ナビゲーション、テーマ、既存 CAD 表示は再設計せず、既存の kernel-integration contract により最新 stable SpatialViewer.CadCore をリリースパッケージへ組み込みます。

## 検証対象

- パッケージには **SpatialViewer.CadCore v0.12.7**（ABI `1.0.0.0`、`SpatialViewer.CadHost 1.0.0` 互換）が組み込まれます。
- Xiangyuan object discovery、native→converted diff、multi-pair candidate consensus、parcel single-variable experiment、Proxy Graphics geometry evidence、reference/endpoint evidence、whole-document A/B matching を含みます。
- whole-document A/B は同一かつ一意の CAD handle と exact class identity のみで対応付けし、座標・layer・text・geometry similarity から推測しません。
- Unknown candidate は global classifier 上で `Unknown` のままです。
- parcel number、land-use、FAR、density、green rate、height、boundary、control-indicator relationship は実サンプルで証明されるまで fail closed のままです。

## 推奨する実機確認

1. 通常 DWG/DXF で既存表示・速度・色・文字・arc・axis/grid・drawing frame の回帰がないこと。
2. 湘源 native drawing で custom object が保持され、利用可能な Proxy Graphics が表示されること。
3. 1つの parcel property または boundary vertex だけを変更したコピーで、対応する構造差分だけが得られること。
4. 湘源の object-to-block / all-explode 等で ordinary CAD を作成し、native drawing との conversion diff を確認すること。
5. 欠落、位置・色・文字異常、crash があれば原図と再現手順を保持すること。

v0.3.8 は v0.13 native Xiangyuan parcel semantics の完成を宣言しません。

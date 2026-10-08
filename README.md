<div align="center">

# RHODES OBS COMMANDER3373

**アークナイツ「統合戦略」のラン情報を、見やすく配信に。**

秘宝・オペレーター・特殊値をまとめて管理し、OBSへ表示するWindows用ツールです。<br>
手動入力、画面認識による入力補助、大会スタッフによる遠隔入力に対応しています。<br>
[CSSによるカスタマイズ](docs/guides/output-css-customization.md)で、フォント・配色・背景・枠線・余白などを細かく調整し、配信画面に合わせた見た目にできます。

[はじめ方](docs/guides/startup-guide.md) · [配布状況](https://github.com/ratedat/RHODES-OBS-COMMANDER3373/releases) · [使い方](docs/README.md) · [不具合・要望](https://github.com/ratedat/RHODES-OBS-COMMANDER3373/issues)

Windows x64 / 日本版アークナイツ向け / 開発・テスト段階

</div>

## 配信での使用例

実際の配信画面を、使用例として紹介しています。画像をクリックすると、その場面から元の配信を視聴できます。

### Rindo3373さん — サルカズの炉辺奇談

[![Rindo3373さんの配信で、画面左にオペレーターと特殊情報、下部に秘宝一覧を表示している使用例](docs/images/stream-rindo-sarkaz.jpg)](https://www.youtube.com/watch?v=DWAcBQgd-Xg&t=7200s)

左側にオペレーター・特殊情報、下部に秘宝を配置した例です。<br>
配信：[Rindo3373【アークナイツ配信他】](https://www.youtube.com/@nekomimikitunemimi) · 2026年10月4日 · [該当場面 2:00:00](https://www.youtube.com/watch?v=DWAcBQgd-Xg&t=7200s)

### 白羽 契さん — 探索者と銀氷の果て

[![白羽 契さんの配信で、ゲーム画面の下部に秘宝アイコンを横並びに表示している使用例](docs/images/stream-shiba-sami.jpg)](https://www.youtube.com/watch?v=FRDEmZXiFWw&t=7200s)

配信枠の下部へ秘宝アイコンを横並びに配置した例です。<br>
配信：[白羽 契 -Shiba Chigiri-](https://www.youtube.com/@%E3%81%97%E3%81%B0%E3%81%A1%E3%81%8E%E3%82%8A) · 2026年8月2日 · [該当場面 2:00:00](https://www.youtube.com/watch?v=FRDEmZXiFWw&t=7200s)

### 東合祭 — 大会配信のオペレーター表示

[![東合祭「溶炉 DAY14」の配信で、画面左側に使用中の星6オペレーターを表示している使用例](docs/images/stream-togosai-operators.jpg)](https://www.youtube.com/watch?v=PuZ4tBOpiHM&t=7200s)

大会配信の左側に、使用中の星6オペレーターをまとめて表示した例です。<br>
配信：[TOGOSEN Univ.](https://www.youtube.com/@LinGuFamily) · 2026年10月4日 · [該当場面 2:00:00](https://www.youtube.com/watch?v=PuZ4tBOpiHM&t=7200s)

画像は配信当時のバージョン・設定による表示です。配置や見た目はカスタマイズできます。出典と権利表記は[紹介画像について](docs/images/README.md)を参照してください。

## できること

- **ラン情報をひとまとめに。** 所持秘宝、招集したオペレーター、分隊、等級、源石錐、ボス、テーマごとの特殊値を管理できます。
- **手動入力ですぐに使える。** ゲームへ接続せず、検索・選択・数値入力だけで配信表示を作れます。認識後の修正も同じ画面から行えます。
- **画面認識で入力を補助。** ADBとMAAFrameworkを使い、ゲーム画面からラン情報を取得できます。
- **配信画面に合わせて配置。** OBSのブラウザソースに対応。透過背景、部品の位置・大きさ、文字や色、一覧の自動スクロールを調整できます。
- **大会の入力を分担。** 入力担当者がブラウザーから情報を送り、配信PCの表示を更新できます。

<details>
<summary>アプリの操作画面を見る</summary>

![オペレーターの検索・選択画面](docs/images/selection.jpg)

*デスクトップアプリの選択画面。名前・職業・レア度などで絞り込み、所持状態を編集できます。*

</details>

## 入手と導入

現在は開発・テスト段階です。**GitHub Releasesでの実行ファイル配布は準備中です。** 公開状況は[Releases](https://github.com/ratedat/RHODES-OBS-COMMANDER3373/releases)で案内します。

配布ZIPをお持ちの場合は、次の手順で始められます。

1. ZIPを新しいフォルダーへ**すべて展開**し、`RhodesSuki.exe` を起動します。
2. 上部で統合戦略テーマを選び、「ラン」「特殊値」「選択」で表示したい情報を入力します。
3. 「出力」で配信サーバーを起動し、**カスタムOverlay**のURLをOBSのブラウザソースへ追加します。幅1920・高さ1080が基準です。

詳しい操作、更新方法、困ったときの確認先は[はじめ方とOBS設定](docs/guides/startup-guide.md)にまとめています。

> GitHubの「Code → Download ZIP」はソースコードです。そのまま起動できる配布版ではありません。ソースから動かす場合は[開発環境の準備](docs/development-setup.md)を参照してください。

## 動作環境

| 用途 | 必要なもの |
| --- | --- |
| アプリの操作 | Windows x64、書き込み可能な展開先 |
| 配信画面への表示 | OBS Studioのブラウザソース、Node.js（アプリから導入可能） |
| 画面認識による入力補助 | ADB接続できるAndroid端末・エミュレーター。認識の基準は1280×720・16:9 |
| 大会の遠隔入力 | 配信PCと入力端末のインターネット接続、入力担当者用のブラウザー |

配布版にはアプリの.NET実行環境を含みます。軽量版は、配信サーバーや大会入力に必要な追加ランタイムを初回利用時に取得します。通常の手動入力にADB接続は不要です。

## 対応する統合戦略

日本版の名称・データを使用します。テーマごとの特殊値も個別に入力・表示できます。

| テーマ | 特殊値の例 |
| --- | --- |
| IS#2 ファントムと緋き貴石 | 幻覚・演目 |
| IS#3 ミヅキと紺碧の樹 | 拒絶反応・灯火・鍵 |
| IS#4 探索者と銀氷の果て | パラダイムロスト・啓示板 |
| IS#5 サルカズの炉辺奇談 | 思案・時代・構想 |
| IS#6 歳の界園志異 | 有効銭・保有銭・遊覧券・歳時 |

## 使い方を探す

| やりたいこと | ガイド |
| --- | --- |
| 初めて起動する・OBSへ表示する | [はじめ方とOBS設定](docs/guides/startup-guide.md) |
| エミュレーターを接続して画面を読み取る | [ADB設定とトラブルシューティング](docs/guides/adb-setup.md) |
| 表示の色・文字・背景を変える | [出力のカスタマイズ](docs/guides/output-css-customization.md) |
| 大会スタッフに入力を任せる | [大会遠隔入力](docs/guides/tournament-remote-input.md) |
| 不具合を報告する・改善を提案する | [不具合報告と機能要望](docs/guides/feedback.md) |

## 現在の制限

- 認識結果には誤りや取りこぼしがあり得ます。取得後に表示内容を確認し、必要に応じて手動で訂正してください。
- **歳の銭OCR（有効銭・保有銭）は停止中です。** 銭は手動入力してください。
- 所持一覧から消えた秘宝の自動削除と、ゲーム内だけで始めた新しいランの確実な自動判定は未対応です。新しいランではアプリ側の「ランをクリア」も使用してください。
- 大会入力の簡易公開URLは、停止またはアプリ終了後に失効します。継続運用する大会では、事前に接続と表示を確認してください。

## 開発・協力

不具合報告、説明の改善、機能の提案も歓迎します。[Issues](https://github.com/ratedat/RHODES-OBS-COMMANDER3373/issues)で受け付けています。

コードやデータの変更に参加する方は[貢献ガイド](CONTRIBUTING.md)、[開発環境の準備](docs/development-setup.md)、[検証手順](docs/development-verification.md)をご覧ください。構成・認識・データ関連の資料は[資料一覧](docs/README.md)から参照できます。

## 謝辞・ライセンス

画面認識には[MAAFramework](https://github.com/MaaXYZ/MaaFramework)、デスクトップUIには[Avalonia](https://github.com/AvaloniaUI/Avalonia)と[SukiUI](https://github.com/kikipoulet/SukiUI)を使用しています。[MaaAssistantArknights](https://github.com/MaaAssistantArknights/MaaAssistantArknights)をはじめとする関連プロジェクトと、データ・素材の提供元に感謝します。

アプリのソースコードは[AGPL-3.0-only](LICENSE)で公開しています。素材・依存ライブラリの出典とライセンスは[第三者表記](THIRD_PARTY_NOTICES.md)および[ライセンスについて](docs/legal/licenses.md)を参照してください。

本ツールはアークナイツの非公式ファンツールです。ゲーム内の名称・画像等の権利は各権利者に帰属します。

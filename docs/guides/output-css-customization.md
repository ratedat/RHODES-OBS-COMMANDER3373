# 出力CSSカスタマイズガイド

このガイドは、RHODES OBS COMMANDER3373のOBS出力をユーザーCSSで調整する方法を説明します。

フォント色、背景色、枠色、文字サイズ、背景の表示・非表示など、一般的な変更は先に画面上の簡易設定を使ってください。

ユーザーCSSは、簡易設定だけでは足りない場合に使う上級者向け機能です。

## CSSを設定する場所

1. アプリの「出力」を開きます。
2. 「統合Overlay設定」または「個別ウィンドウ設定」を開きます。
3. 「上級者向けユーザーCSS」を展開します。
4. CSSを入力します。
5. 「保存して反映」を押します。
6. プレビューまたはOBS Browser Sourceを再読み込みして表示を確認します。

入力しただけでは保存されません。

必ず対象欄の「保存して反映」を押してください。

## 適用範囲

統合Overlayと個別ウィンドウでは、CSSを別々に保存します。

| 設定欄 | 適用先 |
| --- | --- |
| 統合Overlay用ユーザーCSS | `/overlay` と、そのレイアウト切替 |
| 個別ウィンドウ用ユーザーCSS | `/overlay/part/status` など、すべての個別パーツ |

個別ウィンドウ用CSSは、すべての個別パーツで共通です。

特定パーツだけ変更する場合は、後述する `.overlay-part-status` などのルートクラスで対象を限定してください。

背景表示、大会向け表示、タイトル表示には、個別パーツごとの設定が優先される場合があります。

部品の位置と大きさは「ライブレイアウト」で設定します。

ユーザーCSSは、ライブレイアウト上の配置データを変更しません。

## 設定の優先順位

出力には次の順序でスタイルが適用されます。

1. アプリ標準CSS
2. 画面上の簡易設定
3. ユーザーCSS

ユーザーCSSは最後に挿入されるため、通常は同じ詳細度の標準CSSを上書きできます。

必要な場合だけ `!important` を使ってください。

過剰な `!important` は、今後のレイアウト更新や画面上の簡易設定を効きにくくします。

## 対応CSS変数

次の変数は、カスタマイズ用の安定した入口として利用できます。

| 変数 | 内容 | 値の例 |
| --- | --- | --- |
| `--overlay-font-color` | 基本文字色 | `#FFFFFF` |
| `--overlay-background-rgb` | 背景色のRGBチャンネル | `8 11 12` |
| `--overlay-background-alpha` | 背景の不透明度 | `0.85` |
| `--overlay-border-color` | 枠線色 | `#4A5658` |
| `--overlay-accent-color` | 強調色 | `#55D6BE` |
| `--overlay-font-scale` | 全体文字倍率 | `1.1` |

`--overlay-background-rgb` は `rgb()` の引数として使われるため、カンマを入れずに空白で区切ります。

```css
:root {
  --overlay-font-color: #ffffff;
  --overlay-background-rgb: 8 11 12;
  --overlay-background-alpha: 0.88;
  --overlay-border-color: #4a5658;
  --overlay-accent-color: #55d6be;
  --overlay-font-scale: 1.05;
}
```

互換用に `--text`、`--accent`、`--accent-2`、`--line` も設定されますが、新しいCSSでは上表の `--overlay-*` 変数を優先してください。

背景そのものを消す場合は、CSSで透明度だけを変更せず、画面上の「背景を表示」をOFFにしてください。

## 主なルートクラス

表示形式を限定したい場合は、次のクラスを先頭に付けます。

| セレクター | 対象 |
| --- | --- |
| `.overlay-app` | すべてのOverlay |
| `.overlay-compact` | compactレイアウト |
| `.overlay-vertical` | 縦型レイアウト |
| `.overlay-horizontal` | 横型レイアウト |
| `.overlay-full` | fullレイアウト |
| `.overlay-custom` | ライブレイアウト |
| `.overlay-part` | すべての個別ウィンドウ |
| `.overlay-part-status` | ラン状態 |
| `.overlay-part-relics` | 秘宝 |
| `.overlay-part-operators` | オペレーター |
| `.overlay-part-effects` | 発動効果 |
| `.overlay-part-bosses` | ボスフラグ |
| `.overlay-part-special` | 特殊値 |

状態に応じて、`html` 要素へ次のクラスが付きます。

| セレクター | 状態 |
| --- | --- |
| `html.overlay-background-disabled` | 背景表示がOFF |
| `html.overlay-tournament-mode` | 大会向け表示がON |
| `html.overlay-part-titles-hidden` | 個別ウィンドウのタイトル表示がOFF |

## 主な部品セレクター

次のセレクターは、色や文字装飾の調整に利用できます。

| セレクター | 内容 |
| --- | --- |
| `.overlay-card` | 統合Overlayのカード |
| `.overlay-card-header` | カード見出し |
| `.kpi-label` | 数値項目のラベル |
| `.kpi-value` | 数値項目の値 |
| `.relic-tile` | 統合Overlayの秘宝 |
| `.operator-row` | 統合Overlayのオペレーター行 |
| `.operator-name` | オペレーター名 |
| `.boss-card` | ボスカード |
| `.special-overlay-group` | 特殊値のグループ |
| `.special-overlay-chip` | 特殊値の項目 |
| `.overlay-part-shell` | 個別ウィンドウ全体 |
| `.overlay-part-head` | 個別ウィンドウの見出し |
| `.overlay-part-body` | 個別ウィンドウの本文 |
| `.overlay-part-relic` | 個別ウィンドウの秘宝 |
| `.overlay-part-operator` | 個別ウィンドウのオペレーター |
| `.overlay-part-empty` | データがない場合の表示 |

内部構造は今後変わる可能性があります。

長期利用するCSSでは、細かい子要素の階層ではなく、上表のクラスとCSS変数を優先してください。

## 使用例

### 統合Overlayの数値を見やすくする

```css
.kpi-value {
  color: var(--overlay-accent-color);
  font-weight: 800;
  text-shadow: 0 1px 2px rgb(0 0 0 / 70%);
}
```

### オペレーター名を強調する

```css
.operator-name,
.overlay-part-operator strong {
  color: #ffffff;
  font-weight: 700;
}
```

### 個別ウィンドウの見出しだけ変更する

```css
.overlay-part-head {
  color: var(--overlay-accent-color);
  border-bottom-color: var(--overlay-border-color);
  text-transform: none;
}
```

### オペレーター個別ウィンドウだけ変更する

```css
.overlay-part-operators .overlay-part-operator {
  border-color: #59676a;
  background: rgb(8 11 12 / 82%);
}
```

### 特殊値だけ強調色を変える

```css
.overlay-part-special .special-overlay-chip,
.special-overlay-group .special-overlay-chip {
  border-color: #d9ad47;
  color: #fff4cf;
}
```

### 背景OFF時に影も消す

```css
html.overlay-background-disabled .overlay-card,
html.overlay-background-disabled .overlay-part-shell {
  box-shadow: none;
}
```

## 外部フォント、画像、CSSを使う

ユーザーCSSはOverlay描画ページだけへ挿入され、操作アプリ本体のUIには適用されません。

そのため、外部フォント、外部画像、外部CSSを読み込めます。

### Google Fontsを使う

```css
@import url("https://fonts.googleapis.com/css2?family=Noto+Sans+JP:wght@400;700&display=swap");

.overlay-app {
  font-family: "Noto Sans JP", sans-serif;
}
```

### Webフォントを直接指定する

```css
@font-face {
  font-family: "Tournament Sans";
  src: url("https://example.com/fonts/tournament-sans.woff2") format("woff2");
  font-display: swap;
}

.overlay-app {
  font-family: "Tournament Sans", sans-serif;
}
```

### 外部画像を背景に使う

```css
.overlay-app {
  background-image: url("https://example.com/images/overlay-background.png");
  background-position: center;
  background-repeat: no-repeat;
  background-size: cover;
}
```

個別ウィンドウだけへ適用する場合は、個別ウィンドウ用ユーザーCSSへ同じ記述を入力してください。

外部素材はOBSを動かすPCから取得されます。配信中も利用できるHTTPS URLを使い、事前にOBS Browser Sourceで表示を確認してください。

Webフォントは配信元サーバーのCORS設定によって読み込めない場合があります。`@import`形式のWebフォントCSSを利用すると回避できる場合があります。

外部サーバーの停止、URL変更、通信障害が起きると表示が欠けます。大会運用では、重要な画像やフォントを管理下の安定した配信先に置いてください。

## 入力できないCSS

次の記述は拒否されます。

- `javascript:`
- 65,536文字を超えるCSS

通常の`@import`、`url(http://...)`、`url(https://...)`、`@font-face`、`font-family`は使用できます。

## 出力プロファイルの保存

「出力プロファイル (JSON)」のエクスポートには、次の設定がまとめて保存されます。

- 統合Overlay用CSS
- 個別ウィンドウ用CSS
- フォント色、背景色、枠色、強調色
- 文字サイズ
- 背景表示と背景不透明度
- スクロール設定
- 個別パーツ設定
- ライブレイアウトの部品配置

設定を大きく変更する前に、正常なプロファイルをエクスポートしてください。

インポート時はスキーマバージョンを検証します。

将来版で作成された未対応プロファイルや、不正な色・CSSを含むプロファイルは読み込みません。

JSONの概要は次の形です。

```json
{
  "kind": "rhodes-output-profile",
  "schemaVersion": 1,
  "outputPreferences": {
    "schemaVersion": 2,
    "integratedAppearance": {
      "customCss": ""
    },
    "individualAppearance": {
      "customCss": ""
    }
  }
}
```

通常はJSONを直接編集せず、アプリから設定してエクスポートしてください。

## OBSでの確認手順

1. 「出力」で配信サーバーを起動します。
2. 統合Overlayまたは個別ウィンドウのURLをOBS Browser Sourceへ設定します。
3. URL欄の「開く」でブラウザ表示も確認します。
4. CSSを保存した後、OBSのBrowser Sourceを更新します。
5. 反映されない場合は、Browser Sourceのキャッシュを更新するか、ソースを再読み込みします。

統合Overlayと個別ウィンドウは別設定です。

片方だけ変わらない場合は、編集したCSS欄と確認中のURLが一致しているか確認してください。

## 元に戻す方法

最短の復旧方法は、対象のユーザーCSS欄を空にして「保存して反映」を押すことです。

簡易設定も含めて戻す場合は、事前にエクスポートした正常な出力プロファイルをインポートしてください。

CSSの一部だけを確認する場合は、対象範囲を `/*` と `*/` で囲んで一時的に無効化できます。

## トラブルシューティング

| 症状 | 確認内容 |
| --- | --- |
| 保存後も変化しない | 「保存して反映」を押したか、OBS側を再読み込みしたか確認します。 |
| 統合Overlayだけ変化しない | 統合Overlay用CSSへ入力したか確認します。 |
| 個別ウィンドウだけ変化しない | 個別ウィンドウ用CSSへ入力したか確認します。 |
| 一部パーツだけ崩れる | ルートクラスで対象を限定し、固定幅や絶対配置を減らします。 |
| 背景が残る | CSSの透明度ではなく「背景を表示」をOFFにします。 |
| 外部フォントや画像が出ない | URLをブラウザで直接開けるか、HTTPSか、フォント配信元がCORSを許可しているか確認します。OBS Browser Sourceも再読み込みしてください。 |
| 設定を戻せない | CSS欄を空にするか、正常なJSONプロファイルをインポートします。 |

カスタムCSSでレイアウトを大幅に変更する場合は、OBSで実際に使う解像度と、統合Overlay・個別ウィンドウの両方を確認してください。

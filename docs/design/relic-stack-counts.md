# 秘宝スタック数と任意Sidecar同期の実装仕様

## 目的

スタック表示を持つ秘宝の数を、`relicsFull` の同一フレームから安全に取得し、手動修正できる状態として保存して、Suki HUDと全OBS秘宝出力へ `×N` で表示する。

同時に、OBS用Node Sidecarが起動していない通常運用ではローカル保存成功をエラー扱いせず、実際のHTTP/API異常だけを同期失敗として表示する。

## 前提と用語

- ユーザー提示の31件を既知ルールとして `data/relic-stack-rules.json` に保存する。
- 提示一覧には漏れがあり得るため、OCR自体は秘宝名allowlistで制限しない。
- `maximum=null` はゲーム上の既知上限なしを意味する。実装は正の32bit整数を扱う。
- `0` またはキーなしは「未確認」であり、スタック0と断定しない。
- 正規データとの表記差は秘宝IDへ解決する。具体的には「讃歌/賛歌」「Friston.P./Friston.P」「半角/全角コロン」「知識の橋の括弧」をIDで吸収する。

## 状態契約

- `relics`: 所持秘宝ID配列。
- `relicStackCounts`: `{ "<relicId>": <positive integer> }`。
- 所持していない秘宝、0以下、整数でない値、既知上限を超える値は保持しない。
- 秘宝の所持解除・ラン消去時は対応キーを削除する。
- OCR未検出時は既存値を保持し、0へ戻さない。
- ルール未掲載でも汎用OCRで値を得た秘宝は保存し、再読込後は手動修正欄を表示する。

## 認識契約

1. `relicsFull` の秘宝名OCRを現在テーマの秘宝マスターへ一意に解決する。
2. 解決済み秘宝名ボックスをアンカーに、カード左下の明色スタックマーカーを狭いROIで確認する。
3. マーカーがあるカードだけ、括弧欠落等の座標ずれを吸収する広めのROIを6倍拡大・文字検出あり・threshold 0.1でMAA-OCRする。
4. `^9` や `★2` のような結果から数値グループを一つだけ読めた場合に、秘宝IDへCountを付与する。
5. 複数数値、範囲外、OCR失敗は採用しない。既知上限超過は誤読として棄却し、上限値へ丸めない。

## 手動選択と出力

- 既知スタック秘宝を所持選択すると、カード横に数値入力を表示する。
- 上限付きは入力Maximumと説明に上限を反映し、上限なしは「上限なし」と表示する。
- 未掲載秘宝でも保存済みスタック値があれば編集欄を表示する。
- 正の値はSuki HUD、統合Overlay、compact/dense/default、個別秘宝Overlayへ `×N` バッジで表示する。
- Nodeのローカル手動選択画面でも同じ上限ルールを用いる。

## 任意Sidecar同期

- ローカル状態保存は常に第一の成功条件とする。
- 接続拒否・タイムアウト等でSidecarへ到達できない場合は、状態バーを「ローカル状態へ反映 / 配信サーバー未起動」とし、`API同期失敗`とは表示しない。
- HTTPエラーや互換性不一致等、到達後のAPI異常は従来どおり同期失敗として残す。
- 診断証跡には接続エラー詳細を保持する。

## 実装場所

- 正規ルール: `data/relic-stack-rules.json`
- Avaloniaモデル・保存・選択: `apps/rhodes-suki/Models`, `Services`, `ViewModels`, `Views`
- MAA追加OCR: `apps/rhodes-suki/Services/RhodesRelicStackOcrPlanner.cs`
- OBS/ローカルWeb出力: `app/domain`, `app/components`, `app/server.mjs`, `app/app.js`
- C#試験: `tests/rhodes-suki/Program.cs`
- Node試験: `tests/*.test.mjs`

## コマンド

- C#対象試験: `dotnet run --project tests/rhodes-suki/RhodesSuki.ServiceTests.csproj`
- Node全試験: `npm test`
- MAA契約: `npm run maa:check`
- Avaloniaビルド: `dotnet build apps/rhodes-suki/RhodesSuki.csproj --no-restore`
- 差分検査: `git diff --check`

## コード・テスト方針

- 既存のC# record/INotifyPropertyChangedと、Nodeの純関数・`node:test`の流儀へ合わせる。
- ルール整合、上限、未掲載汎用OCR、未検出時保持、所持解除時削除、全Overlay表示、Sidecar未起動表示を小さい回帰試験で固定する。
- 保存済み実機証跡の「呪儀の溯獣=9」「「知識の橋」=2」を認識回帰の基準にする。

## 境界

- 常に行う: 1280x720の正規化フレーム、ID単位の保存、既存値保持、全入力値検証。
- 今回行わない: ゲーム画面の追加操作、個別秘宝を開く操作、依存更新、配布物生成、commit/push。
- 禁止: Android Back keyevent、値の推測、上限超過OCRを上限へ丸めて採用すること。

## 受け入れ条件

- 31件すべてが正規秘宝IDと一致し、指定上限が読み取れる。
- 既知秘宝は選択時にスタック入力でき、上限を超えない。
- `呪儀の溯獣=9`、`「知識の橋」=2`を候補・stateへ反映できる。
- OCR未検出の次回取得で既存値を保持する。
- 所持解除で値が削除される。
- 全OBS秘宝表示とSuki HUDに `×N` が出る。
- Sidecar未起動時はローカル反映成功を明示し、`API同期失敗`を表示しない。
- C#試験、Node試験、MAA契約、Avaloniaビルド、差分検査が成功する。

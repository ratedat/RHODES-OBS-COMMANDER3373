# IS#6 有効銭・保有銭の状態取得 安定化ブリーフ (Claude Code → Codex)

対象セッション: 019f0d1d-8003-7fa1-8b80-87cabec439b3
対象: IS#6 界園の 有効銭 (activeCoins) / 保有銭 (coins) と、それに付属する状態 (coinStatus) の取得安定化。
作業ルート: `O:\Arknights_Rogue_OBSTool`

## 現状の実装 (読了済み、事実)

- profiles: `data/recognition/scan-profiles.json` の `is6ActiveCoinsFull` (run-homeのパネルをトグルタップ→狭域OCR) と `is6CoinsFull` (sui-coin-list、横スクロール右往復→広域OCR+画像認識)
- 候補化: `apps/rhodes-suki/Services/RhodesMaaLocalCandidateConverter.cs` の `CoinCandidates` (2335行〜)。画像認識 (`RhodesSuiCoinImageRecognizer`) とOCRをconfidence maxで併合
- 状態マスタ: `data/selectable-effects.json` slot=coinStatus は **10種のみ**。銭は106種
- 銭名OCRの補助資料: `docs/sui-coin-ocr-corpus.md` (14〜18px合成コーパス)
- 反映: `RhodesRecognitionCandidateApplier` の coin case (`CoinEntryKey(coinId, statusId, face)`)

## 不安定の構造的原因 (コードから特定した仮説、Phase 0で定量化する)

1. **状態→銭の紐付けがY座標ヒューリスティック**: `CoinCandidates` は「銭名行のYより下、次の見出し行Yより上にある最初の状態行」を紐付ける (2485〜2502行)。OCRが1行でも落ちる/順序が揺れると、状態の脱落・隣の銭への誤紐付けが起きる
2. **統合キーが `coinIdstatusId`**: 状態の観測揺れ = 同一銭が複数候補に分裂し、以後絶対に統合されない (2366, 2506行)。スクロール中のボケフレーム1枚の誤状態が独立候補として生き残る
3. **小文字OCR依存**: 銭名14〜18px。名前解決に失敗すると状態行だけが浮き、他の銭に吸われる
4. **パネル開閉が固定座標トグル**: `is6ActiveCoinsFull` のopen/restoreは同一点のタップ。既に開いている/閉じているの検出がなく、位相がズレると別画面をOCRし続ける

## 安定化設計 (この順で実装する)

### A. 紐付けを「行の前後関係」から「スロット幾何」へ変更 【本命】

- 銭一覧は固定レイアウト。パネルの固定UI要素をTemplateMatchでアンカー検出し、アンカー基準で**スロット矩形をインデックス付きで幾何計算**する (1280x720固定なので計算で出る)
- 銭種の判定と状態の判定を**同じスロット矩形の中で完結**させる。行テキストの上下関係には二度と依存しない
- **状態はテンプレ照合を主にする**: coinStatusは10種しかなく、`selectable-effects.json` に image がある。スロット内の状態バッジ固定サブ領域に対する10クラスのTemplateMatchは、小文字OCRより桁違いに安定する。OCRは低信頼時の補助に降格
- 実装位置: `data/recognition/maa-tasks.json` に状態バッジ用タスク(ROI+テンプレ)を追加し、`RhodesSuiCoinImageRecognizer` をスロット幾何+状態サブ領域対応に拡張。テンプレ素材は実機スクショから切り出し (Codexセッションworkの crop guide 手法を流用)

### B. 統合キーの変更と観測合議

- フレーム間の統合キーを **`coinId` (+face)** にし、status は観測の合議で決める: 「最多観測 → 同数なら最高スコア」。既存 `ReconcileOwnedCoinStatusObservations` の考え方を全経路に一般化
- 合議で決まらない場合は **statusId空で候補を出す** (分裂させない)

### C. reducer規則: 空statusで既知statusを上書きしない

- `RhodesRecognitionCandidateApplier` の coin 反映で、既存stateに状態付きで記録済みの銭に対し、statusId空の新候補が来ても状態を消さない (状態は「明示的な別状態の観測」でのみ変更)。現状この規則があるか確認し、無ければ追加+テスト

### D. パネル開閉の状態検出

- `is6ActiveCoinsFull` のopenSteps前に「パネル開閉判定」テンプレ認識を挟み、開いていればタップをスキップ。restoreも同様。scan-profilesのopenStepsに条件分岐が表現できなければ、判定→分岐はSuki側ワークフローで行う

### E. 低信頼スロットの詳細タップフォールバック

- 一覧で銭種または状態が閾値未満のスロットのみ、スロットをタップ→詳細ポップアップの大きい文字をOCR→閉じる。全数巡回はしない (初回フル取得＋以後差分なら速度も許容)
- Back keyeventは禁止 (プロジェクト規約)。閉じるのは画面内タップで

### F. 計測とゴールデン回帰

- **Phase 0 (最初にやる)**: 手持ちの銭画面フレーム (Frame Records + あなたのセッションworkのサンプルPNG) で現行パイプラインをリプレイし、誤りを分類カウントする: ①銭名誤り ②状態誤り ③紐付け誤り(状態が隣の銭に付く) ④候補分裂。**どれが支配的かを数字で出してから A〜E の優先度を確定する**
- 銭画面のゴールデンセット (フレーム＋スロット単位期待値JSON) を作り、閾値・ROI変更の回帰をテスト化。`docs/sui-coin-ocr-corpus.md` の合成コーパスは名前用なので、状態バッジのクロップ集を追加

## 制約 (再掲)

- MAAに任せるのは 接続/撮影/入力/TemplateMatch/OCR/AppendTask/AppendRecognition。状態モデルとreducerはRHODES側
- タップは矩形+ランダム化、Back keyevent禁止
- 1280x720 / 16:9 基準
- 検証: `dotnet build apps\rhodes-suki\RhodesSuki.csproj` / `npm run suki:test` / `npm test` を全緑に保つ。契約テスト (`tests/suki-maaframework-shell.test.mjs`) を壊したら仕様変更として同時更新

## Codexへの依頼

1. このブリーフを読み、現行コード (特に `CoinCandidates` と `RhodesSuiCoinImageRecognizer`、`is6ActiveCoinsFull`/`is6CoinsFull` profile) と突き合わせて、設計A〜Fへの技術的指摘があれば挙げる
2. Phase 0 (誤り分類の計測) の具体的な実行手順を、使用フレーム・コマンド・出力フォーマット込みで提示する
3. A〜E をファイル単位の実装計画 (変更ファイル、追加タスク定義、テンプレ素材の切り出し手順、テスト) に落とす
4. まだコードは変更しない。計画への合意後にユーザーがCodex Desktop上で実装を進める

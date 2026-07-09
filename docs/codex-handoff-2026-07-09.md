# Codex引き継ぎ: 2026-07-09 (Claude Code セッション成果)

対象: RHODES OBS COMMANDER3373 / Avalonia+SukiUI+MAAFramework版 (`apps/rhodes-suki`)
作業ルートは `O:\Arknights_Rogue_OBSTool`。`C:\Users\owner` をプロジェクトルートとして扱わない。
前提ドキュメント: `docs/uiux-current-state-handoff.md` (UI構成)、`docs/public-debug-consultation-questions.md` (公開デバッグ方針)。

## 現在の到達点 (この引き継ぎ時点の働き)

ビルド警告0・エラー0。テストは `npm test` (契約294件) と `npm run suki:test` (サービス110件) が全パス。
変更は**未コミット**(作業ツリー上)。`data/current-state.json` はコミット対象外の作業状態。

## 1. UI再設計 (実施済み)

- MainWindow.axaml をシェル化。ワークスペースは `Views/Workspaces/*.axaml` に分割 (ラン/選択/認識/出力/ランタイム/デバッグ)
- **常設右ペインを廃止** (`Views/Panels/` は削除済み)。プレビュー+候補は認識スタジオ (`RecognitionWorkspaceView`) に統合。ROI編集群はExpander「ROI調整 (開発者向け)」へ
- ラン=ホームダッシュボード: 最新キャプチャ + プロファイル別「認識+反映」 + ラン値カード + 候補リスト
- 左ナビはListBox化 (`SelectedWorkspaceNavItem` で選択状態常時表示)。ヘッダーは IS選択 / 接続ピル (診断由来の色+ラベル、クリックで接続画面へ) / 主アクション (撮影・認識+反映・報告ZIP・HUD)
- デザイントークンは `Views/WorkbenchTheme.axaml` (`Button.primary`, `Border.card/.panel`, `TextBlock.h1/.h2/.caption/.mono`, `ListBox.navList`, `ItemsControl.choicePane`)

## 2. オペレーター/秘宝選択の修正 (実施済み・重要バグ3件)

1. **状態保存の実バグ**: `JsonArray.Add<string>` が作る `JsonValueCustomized` は、`TypeInfoResolver` の無い `JsonSerializerOptions` を渡した `ToJsonString` で必ず
   `InvalidOperationException (must specify a TypeInfoResolver)` になる。修正: `RhodesRunStateStore.WriteOptions` と `RhodesStateApiClient.IndentedWriteOptions` に
   `TypeInfoResolver = new DefaultJsonTypeInfoResolver()` を設定。保存失敗は `Debug Logs/state-save-errors.log` にスタック付きで記録される (恒久)
2. **ドロップダウン選択不可**: MainWindowの「外側クリックで全ComboBoxを閉じる」独自ハンドラがポップアップ内クリックも閉じていた → 撤去 (light dismissに委譲)。復活させないこと (契約テストで doesNotMatch 固定)
3. **コンボ表示の固着**: 選択肢リスト再構築時にComboBoxがnullを押し込み、VMが同値正規化して通知ゼロ → 表示だけ空に固着。`EnsureFilterValue` が**値が変わらなくても必ず再通知**する仕様に変更。列数コンボはint項目のSelectionBox表示不具合を避け **NumericUpDown** に置換
- その他: 選択リストは **ListBox→ItemsControl** (行コンテナの選択/フォーカス枠を根絶、仮想化はテンプレートの ScrollViewer+VirtualizingStackPanel で維持)。カードは `Classes.selected/.excluded` で視覚状態 (ティール枠/減光)。画像は `Bitmap.DecodeToWidth(96)` サムネイルデコード。検索は250msデバウンス。クリック時の全再構築は「選択のみ/除外を隠す」時に限定 (優先表示の並び替えは次回リスト再構築時に反映 — 仕様変更、テスト更新済み)

## 3. 段階診断・接続まわり (実施済み)

- `RhodesAdbDiagnosticsChecklist`: 6段階 (ADB実行ファイル→ADB起動→端末検出→MAA接続→スクショ→解像度1280x720/16:9)、`未確認/OK/注意/失敗`+次の行動。「診断コピー」でテキスト化 (報告用)
- ヘッダー接続ピル (`ConnectionStatus*`) は診断状態から導出

## 4. 共通値の手動入力 + 等級⇔多元化珍品 (実施済み)

- ラン画面「手動入力」: 源石錐(0-9999)/等級(1-15)/分隊(現IS分隊コンボ)。**認識と同一の反映パイプライン** `ApplyCandidatesPipelineAsync` を通る (手動値=手動候補)
- **等級→run.difficultyTierId の導出をSukiローカルに実装** (`RhodesDifficultyTierCatalog`、`data/difficulty-tiers.json` 準拠、Web側 `app/domain/difficulty.js` と同一規則)。従来はAPIサーバー経由でしか導出されず、オフラインでは多元化珍品バリアントが追従しなかった。候補反映 (`RhodesRecognitionCandidateApplier` の difficulty case) とIS変更時の破棄 (`ResetRunValues` に difficultyTierId 追加) を実装。境界値テストあり (IS5: 現実的0-2/創作的3-5/幻想的6-8/空想的9+)
- 珍品の最終バリアント解決 (`effectCalculation.variantResolution.activeVariantRelicIds`) はAPIサーバー側の担当のまま

## 5. 配信サーバー / OBS部品 (実施済み)

- overlay/sidecar非動作の正体は「サーバー起動手段がSukiに無い」こと。`RhodesSidecarServerLauncher` を新設し、出力ワークスペースの「配信サーバー」カードから node `app/server.mjs` を起動/停止/状態表示 (script探索は dataRoot親→実行フォルダから上方向)
- **部品単位URL** `/overlay/part/{status|relics|operators|effects|bosses|special}` はサーバー側に既存 (`app/lib/overlay-config.js`)。`RhodesOverlayPartLinkCatalog` で出力画面に一覧化 (OBSに個別ブラウザソースとして追加=チェリーピッキング配置)。`?size=s/l` で密度可変。status部品からBossセルと更新時刻セルを削除 (`app/components/overlay-parts.js`)
- レスポンシブとOBS: ソースのプロパティ幅高さ=リフロー (推奨) / 変形ハンドル=ピクセル拡縮

## 6. HUD (本人用透過小窓) — Phase 1+表示制御 (実施済み)

- 設計: サイドカー(Web)は第2画面操作用、視聴者向けはOBSオーバーレイ、**本人用はネイティブHUD** の3面に役割分離。MAA本家のログ小窓 (v6.1.0+) と同型のデスクトップ最前面ウィンドウ方式。エミュ内には描画しないためADBスクショ(認識対象)に写り込まない
- `Views/HudWindow.axaml(.cs)`: 枠なし/透過/最前面/非アクティブ表示。グリップドラッグ移動、**クリック透過** (Win32 `WS_EX_TRANSPARENT`、復帰はHUDトグルOFF→ON)、位置は settings に保存
- **表示内容制御**: `RhodesHudPartCatalog` (源石錐/IS特殊値/等級/分隊/多元化珍品Tier/直近の反映結果/接続状態[既定OFF])。出力ワークスペースの「HUD (本人用小窓)」カードのチェックで即時反映、`suki-settings.json` の `HudVisibleParts` に永続化。チップは `HeaderStatusChips` を `chip.Detail` (=RunFieldRegistryのHeaderDetailId) でフィルタした `HudChips`
- ライフサイクル: `MainWindowViewModel.IsHudVisible` をMainWindowコードビハインドが監視して生成/表示/非表示。設定 `HudVisible/HudX/HudY/HudVisibleParts` は `RhodesSukiSettings` に追加済み
- Phase 2候補 (未着手): フリーテキストメモ/チェックリスト、エミュレータウィンドウ追従 (MaaToolkit `IMaaToolkitDesktop` のウィンドウ列挙を利用予定)、透明度スライダー、ホットキー

## 7. デバッグ基盤 (以前からの分も含む現状)

- 記録: FrameRecordStore (frameId+PNG+state snapshot)、報告ZIP (BugReportBundle)、**ZIP/フォルダ取込** (ImportBugReport*)、**保存Frame再認識** (`AppendRecognition` + `RunResourceRecognitionAsync`) = ADBなしリプレイ
- 候補反映は outcome (applied/ignored+reason) を記録 (`SukiCandidateApplyOutcome`)
- 公開デバッグ制約: IS#5サルカズのみ、profileは runStatusFull/operatorsFull/relicsFull/is5ThoughtFull/is5AgeFull (`RhodesPublicDebugPolicy`)

## 未了・次候補 (優先順の提案)

1. HUD Phase 2 (メモ/チェックリスト → エミュ追従)
2. 報告ZIPへ当時のresource定義 (rhodes.json/rhodes-generated.json) とSHA256をmanifest同梱
3. ゴールデンセット化 (思案→時代→秘宝→オペの順) を `suki:test` に組込み
4. 全部入り `/overlay` ヘッダーのBossチップ削除 (部品版は削除済み、全部入り側は未対応・保留中)

## 検証コマンド

```powershell
cd O:\Arknights_Rogue_OBSTool
dotnet build apps\rhodes-suki\RhodesSuki.csproj   # 0 warning / 0 error
npm run suki:test                                  # 110件
npm test                                           # 294件
npm run dev                                        # 配信サーバー (Sukiの出力画面「起動」でも可)
```

## 守るべき決まり (再掲)

- Electron/Tauri/旧OCRは復活させない。希望/耐久値/シールド/指揮LvはUIに戻さない
- MAAはADB接続/撮影/入力/OCR/AppendTask/AppendRecognitionまで。state reducer/OBS出力/報告基盤はRHODES側
- 契約テスト (`tests/suki-maaframework-shell.test.mjs`) が構造仕様を固定している。UI構造を変えたらテストも同時に更新すること

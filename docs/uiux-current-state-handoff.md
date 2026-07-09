# UI/UX Current State Handoff

Date: 2026-07-04

This document is a handoff for UI/UX-only improvement work on RHODES OBS COMMANDER3373.
The active project root is `O:\Arknights_Rogue_OBSTool`. Do not treat `C:\Users\owner` as the project root.

## 目的

RHODES OBS COMMANDER3373 の現行UIは、一般的な設定画面ではなく、Arknights Integrated Strategies のラン状態、ADB/MAA接続、認識結果、OBS出力、デバッグ報告を扱う検証ワークベンチです。

UI/UX改善では、見た目だけでなく次の作業を迷わず行えることを優先します。

- ADB/MAA接続を設定、診断、撮影する。
- MAA-OCR/テンプレート認識を実行する。
- 保存スクショ、認識ログ、候補、state反映差分を追う。
- オペレーターと秘宝を高速に検索、フィルター、直接選択する。
- OBS/サイドカーへ必要な表示だけを出す。
- バグ報告ZIPを作り、別環境で再現できる情報を残す。

## 絶対前提

- 現行UIターゲットは Avalonia/SukiUI です。
- Electron/Tauri は今後のUI改善対象外です。
- OCRは MAA-OCR を既定にします。
- GLM-OCR/Ollama は任意ダウンロード/任意導入です。
- 旧OCR経路は復活させません。
- 対応基準は 16:9 / 1280x720 です。
- 認識対象は `源石錐`, `等級`, `分隊`, `ISごとの特殊値`, `オペレーター`, `秘宝` です。
- `希望`, `耐久値`, `シールド`, `指揮Lv` は公開デバッグ対象から外しています。UIに戻さないでください。
- 配布物、検証用EXE、デバッグZIPは、ユーザーが明示した時だけ作成します。

## 現行UIの情報設計

現行の大枠は 4領域です。

| 領域 | 役割 | 主な実装 |
| --- | --- | --- |
| Top context header | アプリ名、IS選択、ラン概要、ADB状態、保存/接続/撮影 | `apps/rhodes-suki/Views/MainWindow.axaml` |
| Left workspace nav | `ラン`, `選択`, `認識`, `出力`, `ランタイム`, `デバッグ` | `RhodesWorkspaceRegistry.cs` |
| Center workspace | 選択中ワークスペースの主操作 | `MainWindow.axaml`, `MainWindowViewModel.cs` |
| Right inspector | 選択中の証跡、候補、スクショ、診断 | `MainWindow.axaml`, `RefreshInspectorRows()` |

ワークスペースは固定です。新しいIS固有値、認識プロファイル、出力部品、ランタイム機能が増えても、トップレベルのタブを増やすのではなく、この6領域内のセクション、サブパネル、インスペクタで扱います。

## ワークスペース仕様

### ラン

目的: 現在ランの取得値とIS固有値を確認、編集、認識へ送る。

扱う値:

- 源石錐
- 等級
- 分隊
- IS固有値
  - IS#3: 啓示、呼び声など
  - IS#4: 灯火、崩壊値、啓示板など
  - IS#5: 思案、構想、時代など
  - IS#6: コイン、時間系など

関連ファイル:

- `apps/rhodes-suki/Services/RhodesRunCatalog.cs`
- `apps/rhodes-suki/Services/RhodesRunFieldRegistry.cs`
- `apps/rhodes-suki/Services/RhodesRunStateStore.cs`
- `apps/rhodes-suki/Models/RunCatalogModels.cs`
- `data/current-state.json`
- `data/campaigns.json`
- `data/squads.json`
- `data/difficulty-grades.json`
- `data/difficulty-tiers.json`

主なUIバインディング:

- `Campaigns`
- `SelectedCampaign`
- `RunFieldPreviews`
- `SpecialValuePreviews`
- `CampaignPreviews`
- `RunContextSummary`

### 選択

目的: オペレーターと秘宝を検索、フィルター、直接選択する。

現在の問題:

- 旧UIより操作性が落ちています。
- 選択と表示除外のボタンは不要です。
- 直接クリックで選択する形に戻す必要があります。
- フィルター、レアリティ、職業、職分の並びと表示が不安定です。
- 選択が重く、仮想化または更新差分の見直しが必要です。
- 4ペインまで選べる表示が必要です。
- タグ表示は不要です。

関連ファイル:

- `apps/rhodes-suki/Services/RhodesChoiceCatalogRegistry.cs`
- `apps/rhodes-suki/Services/RhodesChoiceFilter.cs`
- `apps/rhodes-suki/Services/RhodesChoiceRows.cs`
- `apps/rhodes-suki/Services/RhodesOperatorTaxonomy.cs`
- `apps/rhodes-suki/ViewModels/MainWindowViewModel.cs`
- `data/operators.json`
- `data/operator-images.json`
- `data/operator-implementation-history.json`
- `data/relics.json`
- `data/relic-images.json`
- `data/relic-effect-variants.json`
- `data/relic-effect-rules.json`

主なUIバインディング:

- `FilteredOperatorRows`
- `FilteredRelicRows`
- `OperatorSearch`
- `RelicSearch`
- `OperatorRarityOptions`
- `OperatorClassOptions`
- `OperatorBranchOptions`
- `RelicCategoryOptions`
- `OperatorPaneColumns`
- `RelicPaneColumns`
- `OperatorListSummary`
- `RelicListSummary`

推奨UI:

- 左右または最大4ペインの密なリスト。
- 検索欄、レアリティ、職業、職分、カテゴリだけを目立たせる。
- 行またはカードの直接クリックで選択状態を切り替える。
- 表示除外は別ボタンではなく、必要ならフィルター設定側に隠す。
- 長い効果文は一覧で詰め込まず、右インスペクタへ逃がす。

### 認識

目的: MAA Resource taskを実行し、候補化し、stateへ反映する。

現在の主な流れ:

1. 認識プロファイルを選ぶ。
2. MAA Resource taskを実行する。
3. Resource task結果を保存する。
4. 候補へ変換する。
5. 候補をstate/APIへ反映する。
6. ログ、ROI、OCR詳細、スクショを確認する。

関連ファイル:

- `apps/rhodes-suki/Services/RhodesRecognitionWorkspaceRegistry.cs`
- `apps/rhodes-suki/Services/RhodesRecognitionWorkflow.cs`
- `apps/rhodes-suki/Services/RhodesRecognitionCandidateApplier.cs`
- `apps/rhodes-suki/Services/RhodesRecognitionScanHistory.cs`
- `apps/rhodes-suki/Services/RhodesRecognitionProbe.cs`
- `apps/rhodes-suki/Services/RhodesMaaResourceCatalog.cs`
- `apps/rhodes-suki/Services/RhodesMaaRecognitionPolicy.cs`
- `apps/rhodes-suki/Services/RhodesMaaLocalCandidateConverter.cs`
- `apps/rhodes-suki/Services/RhodesMaaCandidateMerger.cs`
- `apps/rhodes-suki/Services/RhodesMaaCandidateApiClient.cs`
- `apps/rhodes-suki/Services/RhodesMaaRecognitionEvidenceLog.cs`
- `apps/rhodes-suki/resource/base/pipeline/rhodes.json`
- `apps/rhodes-suki/resource/base/pipeline/rhodes-generated.json`
- `data/recognition/maa-operator-name-ocr.json`
- `data/recognition/maa-onnx-ocr-assets.json`
- `data/recognition/maa-ocr-rules.json`

主なUIバインディング:

- `ResourceProfiles`
- `SelectedResourceProfile`
- `ResourceTasks`
- `ResourceTaskResults`
- `CandidateResults`
- `RecognitionScanHistory`
- `RecognitionScanLogRows`
- `RecognitionDetailJson`
- `OcrDetailRows`
- `RoiDetailRows`
- `ProbePayloads`
- `ProbeResults`

必要なUI改善:

- `認識実行`, `候補確認`, `反映差分`, `証跡` を同じ画面に詰め込みすぎない。
- 候補0件、候補あり/反映なし、反映値違いを別状態として表示する。
- `frame_id` 単位でスクショ、認識結果、候補、state差分を追えるようにする。
- 右インスペクタで Raw OCR、ROI、Candidate、Applied state を辿れるようにする。

### 出力

目的: OBS/サイドカー/大会表示向けに表示部品を制御する。

関連ファイル:

- `apps/rhodes-suki/Services/RhodesOutputPartRegistry.cs`
- `apps/rhodes-suki/Services/RhodesPreviewUrlBuilder.cs`
- `apps/rhodes-suki/Models/MaaSessionModels.cs`
- `apps/rhodes-suki/ViewModels/MainWindowViewModel.cs`

主なUIバインディング:

- `OutputParts`
- `OutputSeparateWindow`
- `OutputTournamentMode`
- `OutputTransparentBackground`
- `OutputScrollSpeed`

UI方針:

- 認識・選択・出力を混ぜない。
- OBSに出るもの、サイドカーに出るもの、内部デバッグだけのものを分ける。
- 出力部品ごとに表示、スクロール、除外反映を明示する。

### ランタイム

目的: ADB、MAAFramework、キャプチャ方式、入力方式、任意OCR、Hyper-Vを管理する。

現在の問題:

- ADB UI/UXが分かりにくい。
- 自動検出、手動入力、候補使用、端末選択、接続テスト、撮影方式の関係が見えにくい。
- `DLL not found`, VC++不足、ADB拒否、offline、複数端末などの原因別表示がまだ弱い。
- MuMu高速撮影などMAAFramework側のADBオプションを、ユーザーが自然に選べる形にする必要があります。

関連ファイル:

- `apps/rhodes-suki/Services/RhodesAdbPresetCatalog.cs`
- `apps/rhodes-suki/Services/RhodesAdbCandidateRegistry.cs`
- `apps/rhodes-suki/Services/RhodesAdbLocalDetector.cs`
- `apps/rhodes-suki/Services/RhodesAdbDeviceProbe.cs`
- `apps/rhodes-suki/Services/RhodesAdbApiClient.cs`
- `apps/rhodes-suki/Services/RhodesSukiAdbDetectionWorkflow.cs`
- `apps/rhodes-suki/Services/RhodesSukiAdbConnectionTestWorkflow.cs`
- `apps/rhodes-suki/Services/RhodesSukiRuntimeProbeWorkflow.cs`
- `apps/rhodes-suki/Services/RhodesRuntimeWorkspaceRegistry.cs`
- `apps/rhodes-suki/Services/RhodesRuntimeCapabilityRegistry.cs`
- `apps/rhodes-suki/Services/RhodesOptionalRuntimeProbe.cs`
- `apps/rhodes-suki/Services/RhodesSukiOptionalRuntimeActionWorkflow.cs`
- `apps/rhodes-suki/Services/RhodesHypervisorProbe.cs`
- `apps/rhodes-suki/Services/MaaFrameworkRuntimeProbe.cs`
- `apps/rhodes-suki/Services/RhodesMaaSession.cs`
- `apps/rhodes-suki/Services/RhodesMaaPaths.cs`

主なUIバインディング:

- `AdbPresets`
- `SelectedAdbPreset`
- `AdbPath`
- `AdbSerial`
- `AdbPathCandidates`
- `SelectedAdbPathCandidate`
- `AdbDevices`
- `RuntimeCapabilities`
- `CaptureMethodLabel`
- `InputMethodLabel`
- `AdbHeaderTitle`
- `AdbHeaderSubtitle`

推奨UI:

- 段階診断式にする。
  - `1. ADB実行ファイル`
  - `2. 端末検出`
  - `3. MAA接続`
  - `4. スクショ取得`
  - `5. 解像度 1280x720 / 16:9`
- 各段階を `OK`, `注意`, `失敗`, `未確認` で表示する。
- Google Play Games, MuMu, LDPlayer, 汎用ADBをプリセットとして分ける。
- MuMu/LD向け高速撮影、標準ADB撮影、互換撮影をユーザーが選べるようにする。
- 手動入力は最後の逃げ道として残し、既定は候補選択に寄せる。

### デバッグ

目的: 公開デバッグで報告者から届いた情報を再現できるようにする。

関連ファイル:

- `apps/rhodes-suki/Services/RhodesSukiDebugPaths.cs`
- `apps/rhodes-suki/Services/RhodesFrameRecordStore.cs`
- `apps/rhodes-suki/Services/RhodesBugReportBundle.cs`
- `apps/rhodes-suki/Services/RhodesMaaRecognitionEvidenceLog.cs`
- `apps/rhodes-suki/ViewModels/MainWindowViewModel.cs`
- `docs/debugger-adb-report-guide.md`

主なUIバインディング:

- `LastCaptureImage`
- `LastCapturePath`
- `LastResourceTaskResultsPath`
- `LastBugReportBundlePath`
- `BugReportBundleStatus`
- `RecognitionScanHistory`
- `RecognitionScanLogRows`

デバッグ成果物の基本場所:

- `RHODES OBS COMMANDER3373 Debug Logs`
- `Recognition Scans`
- `Frame Records`
- `ROI Sessions`
- `ROI Drafts`
- `Bug Reports`
- `maa-captures`

これらは実行フォルダ基準で作られます。配布ZIPや検証フォルダによってベースパスは変わります。

## 2026-07-08 UI再設計 (実施済み)

分割済みViewを前提に、レイアウトと操作設計を作り直した。以降のUI作業は該当Viewファイルを直接編集する。

| 画面 | ファイル |
| --- | --- |
| シェル (ヘッダー/ナビ/フッター) | `apps/rhodes-suki/Views/MainWindow.axaml` |
| 共有スタイル/デザイントークン | `apps/rhodes-suki/Views/WorkbenchTheme.axaml` (App.axamlからStyleInclude) |
| ラン=ホームダッシュボード | `apps/rhodes-suki/Views/Workspaces/RunWorkspaceView.axaml` |
| 選択 | `apps/rhodes-suki/Views/Workspaces/ChoicesWorkspaceView.axaml` |
| 認識スタジオ (プレビュー/ROI/候補/証跡) | `apps/rhodes-suki/Views/Workspaces/RecognitionWorkspaceView.axaml` (+.cs にROI操作) |
| 出力 | `apps/rhodes-suki/Views/Workspaces/OutputWorkspaceView.axaml` |
| 接続 (段階診断含む) | `apps/rhodes-suki/Views/Workspaces/RuntimeWorkspaceView.axaml` |
| デバッグ (報告取込/Frame再認識/Probe) | `apps/rhodes-suki/Views/Workspaces/DebugWorkspaceView.axaml` |
| 段階診断ロジック | `apps/rhodes-suki/Services/RhodesAdbDiagnosticsChecklist.cs` |

設計判断:

- **常設右ペイン(インスペクタ/プレビュー・結果)を廃止**。`Views/Panels/` は削除済み。プレビューと候補は認識スタジオの主役として大きく表示し、全ワークスペースが全幅を使う。
- **ヘッダーは4要素のみ**: IS選択 / 接続ピル(診断状態から色+ラベル、クリックで接続画面へ) / 主アクション(撮影・認識+反映・報告ZIP)。ラン値チップと保存/接続ボタンはヘッダーから撤去(保存・接続は接続画面にある)。
- **左ナビはListBox化し選択中ワークスペースを常時ハイライト** (`SelectedWorkspaceNavItem`)。
- **ラン=ホーム**: 最新キャプチャ+プロファイル別「認識+反映」ボタン+ラン値カード+候補リスト。配信中はこの画面だけで回る。
- **認識スタジオ**: 左=大プレビュー(ROIオーバーレイ)+タスク個別実行/ROI調整(Expander、既定で閉じる)、右=候補/反映結果/スキャン履歴/Raw OCR/task結果(Expander)。
- **開発者向けツールはExpanderに退避**: ROI調整、認識Probe(デバッグ画面へ移設)。公開デバッグ配布時に非表示化しやすい構造。
- **デザイントークン** (WorkbenchTheme): `Button.primary`(主アクション、1画面1つ)、`Border.card`/`Border.panel`、`TextBlock.h1/.h2/.caption/.mono`(文字3段階、9-10px廃止)、`ListBox.navList`。状態色は診断と同一の4値トーン(OK緑/注意琥珀/失敗赤/未確認灰)。
- フッターに `IsBusy` の処理中インジケータを追加。
- P0「ADB/Runtime段階診断」はランタイム(接続)画面最上部に実装済み。`AdbDiagnosticSteps` 6段階+次の行動、診断テキストコピー付き。

## 実装ファイルマップ

### Avalonia/SukiUI本体

| 用途 | ファイル |
| --- | --- |
| アプリ定義 | `apps/rhodes-suki/App.axaml` |
| アプリ起動処理 | `apps/rhodes-suki/App.axaml.cs` |
| エントリポイント | `apps/rhodes-suki/Program.cs` |
| プロジェクト定義 | `apps/rhodes-suki/RhodesSuki.csproj` |
| シェルXAML | `apps/rhodes-suki/Views/MainWindow.axaml` |
| シェルコードビハインド | `apps/rhodes-suki/Views/MainWindow.axaml.cs` |
| ワークスペースView | `apps/rhodes-suki/Views/Workspaces/*.axaml` |
| 右ペインView | `apps/rhodes-suki/Views/Panels/*.axaml` |
| 共有テーマ | `apps/rhodes-suki/Views/WorkbenchTheme.axaml` |
| メイン画面ViewModel | `apps/rhodes-suki/ViewModels/MainWindowViewModel.cs` |
| 非同期コマンド | `apps/rhodes-suki/ViewModels/AsyncRelayCommand.cs` |
| MAA interface定義 | `apps/rhodes-suki/interface.json` |

### UI構造・ワークスペース

| 用途 | ファイル |
| --- | --- |
| 左ナビのワークスペース定義 | `apps/rhodes-suki/Services/RhodesWorkspaceRegistry.cs` |
| 各ワークスペースの見出し/セクション | `apps/rhodes-suki/Services/RhodesWorkspaceLayoutRegistry.cs` |
| ワークスペース別アクション | `apps/rhodes-suki/Services/RhodesWorkspaceActionRegistry.cs` |
| プロダクト面の定義 | `apps/rhodes-suki/Services/RhodesProductSurfaceRegistry.cs` |

### ラン状態

| 用途 | ファイル |
| --- | --- |
| ランカタログ構築 | `apps/rhodes-suki/Services/RhodesRunCatalog.cs` |
| ランフィールド定義 | `apps/rhodes-suki/Services/RhodesRunFieldRegistry.cs` |
| state保存/読込 | `apps/rhodes-suki/Services/RhodesRunStateStore.cs` |
| state/API同期 | `apps/rhodes-suki/Services/RhodesSukiStateSyncWorkflow.cs` |
| APIクライアント | `apps/rhodes-suki/Services/RhodesStateApiClient.cs` |
| ラン/ワークスペース/選択行モデル | `apps/rhodes-suki/Models/RunCatalogModels.cs` |

### オペレーター/秘宝選択

| 用途 | ファイル |
| --- | --- |
| 選択カタログ構築 | `apps/rhodes-suki/Services/RhodesChoiceCatalogRegistry.cs` |
| フィルター処理 | `apps/rhodes-suki/Services/RhodesChoiceFilter.cs` |
| 表示行構築 | `apps/rhodes-suki/Services/RhodesChoiceRows.cs` |
| 職業/職分順 | `apps/rhodes-suki/Services/RhodesOperatorTaxonomy.cs` |

### 認識/MAA Resource

| 用途 | ファイル |
| --- | --- |
| 認識ワークスペース定義 | `apps/rhodes-suki/Services/RhodesRecognitionWorkspaceRegistry.cs` |
| 認識実行フロー | `apps/rhodes-suki/Services/RhodesRecognitionWorkflow.cs` |
| 候補のstate反映 | `apps/rhodes-suki/Services/RhodesRecognitionCandidateApplier.cs` |
| 認識履歴 | `apps/rhodes-suki/Services/RhodesRecognitionScanHistory.cs` |
| 認識プローブ | `apps/rhodes-suki/Services/RhodesRecognitionProbe.cs` |
| MAAタスク一覧/プロファイル | `apps/rhodes-suki/Services/RhodesMaaResourceCatalog.cs` |
| MAA認識ポリシー | `apps/rhodes-suki/Services/RhodesMaaRecognitionPolicy.cs` |
| ローカル候補化 | `apps/rhodes-suki/Services/RhodesMaaLocalCandidateConverter.cs` |
| 候補マージ | `apps/rhodes-suki/Services/RhodesMaaCandidateMerger.cs` |
| 候補API | `apps/rhodes-suki/Services/RhodesMaaCandidateApiClient.cs` |
| 認識証跡保存 | `apps/rhodes-suki/Services/RhodesMaaRecognitionEvidenceLog.cs` |
| MAA task診断 | `apps/rhodes-suki/Services/RhodesMaaTaskDiagnostics.cs` |
| OCR詳細行 | `apps/rhodes-suki/Services/RhodesMaaOcrDetailRows.cs` |
| MAA結果プレビュー | `apps/rhodes-suki/Services/RhodesMaaResultPreview.cs` |

### ROI/テンプレート調整

| 用途 | ファイル |
| --- | --- |
| ROI選択マッチ | `apps/rhodes-suki/Services/RhodesMaaRoiSelectionMatcher.cs` |
| ROIプレビュー投影 | `apps/rhodes-suki/Services/RhodesMaaRoiPreviewProjector.cs` |
| ROI詳細行 | `apps/rhodes-suki/Services/RhodesMaaRoiDetailRows.cs` |
| ROI調整セッションログ | `apps/rhodes-suki/Services/RhodesMaaRoiAdjustmentSessionLog.cs` |
| ROIドラフトログ | `apps/rhodes-suki/Services/RhodesMaaRoiEditDraftLog.cs` |
| ROIドラフト反映 | `apps/rhodes-suki/Services/RhodesMaaRoiDraftSourceUpdater.cs` |
| ROIドラフト一括反映 | `apps/rhodes-suki/Services/RhodesMaaRoiDraftBatchSourceUpdater.cs` |

### ADB/Runtime

| 用途 | ファイル |
| --- | --- |
| ADBプリセット | `apps/rhodes-suki/Services/RhodesAdbPresetCatalog.cs` |
| ADB候補正規化 | `apps/rhodes-suki/Services/RhodesAdbCandidateRegistry.cs` |
| ローカルADB検出 | `apps/rhodes-suki/Services/RhodesAdbLocalDetector.cs` |
| ADB端末probe | `apps/rhodes-suki/Services/RhodesAdbDeviceProbe.cs` |
| ADB APIクライアント | `apps/rhodes-suki/Services/RhodesAdbApiClient.cs` |
| ADB自動検出ワークフロー | `apps/rhodes-suki/Services/RhodesSukiAdbDetectionWorkflow.cs` |
| ADB接続テスト | `apps/rhodes-suki/Services/RhodesSukiAdbConnectionTestWorkflow.cs` |
| Runtime probe | `apps/rhodes-suki/Services/RhodesSukiRuntimeProbeWorkflow.cs` |
| Runtime画面定義 | `apps/rhodes-suki/Services/RhodesRuntimeWorkspaceRegistry.cs` |
| Runtime能力カード | `apps/rhodes-suki/Services/RhodesRuntimeCapabilityRegistry.cs` |
| 任意runtime probe | `apps/rhodes-suki/Services/RhodesOptionalRuntimeProbe.cs` |
| GLM/Ollama任意導入操作 | `apps/rhodes-suki/Services/RhodesSukiOptionalRuntimeActionWorkflow.cs` |
| Hyper-V確認 | `apps/rhodes-suki/Services/RhodesHypervisorProbe.cs` |
| MAAFramework runtime確認 | `apps/rhodes-suki/Services/MaaFrameworkRuntimeProbe.cs` |
| MAA session | `apps/rhodes-suki/Services/RhodesMaaSession.cs` |
| MAAパス解決 | `apps/rhodes-suki/Services/RhodesMaaPaths.cs` |

### OBS/出力

| 用途 | ファイル |
| --- | --- |
| 出力部品定義 | `apps/rhodes-suki/Services/RhodesOutputPartRegistry.cs` |
| プレビューURL | `apps/rhodes-suki/Services/RhodesPreviewUrlBuilder.cs` |

### デバッグ/再現基盤

| 用途 | ファイル |
| --- | --- |
| デバッグ保存先 | `apps/rhodes-suki/Services/RhodesSukiDebugPaths.cs` |
| フレーム記録 | `apps/rhodes-suki/Services/RhodesFrameRecordStore.cs` |
| バグ報告ZIP | `apps/rhodes-suki/Services/RhodesBugReportBundle.cs` |
| 画像パス変換 | `apps/rhodes-suki/Services/RhodesBitmapPathConverter.cs` |
| API状態probe | `apps/rhodes-suki/Services/RhodesApiStatusProbe.cs` |

### モデル

| 用途 | ファイル |
| --- | --- |
| ラン/選択/インスペクタ/ワークスペースモデル | `apps/rhodes-suki/Models/RunCatalogModels.cs` |
| MAA/ADB/認識/候補/出力/ROIモデル | `apps/rhodes-suki/Models/MaaSessionModels.cs` |
| 統合状態 | `apps/rhodes-suki/Models/IntegrationStatus.cs` |

## データファイルマップ

| 用途 | ファイル |
| --- | --- |
| オペレーターマスタ | `data/operators.json` |
| オペレーター画像 | `data/operator-images.json` |
| オペレーター実装履歴 | `data/operator-implementation-history.json` |
| 秘宝マスタ | `data/relics.json` |
| 秘宝画像 | `data/relic-images.json` |
| 秘宝効果variant | `data/relic-effect-variants.json` |
| 秘宝効果ルール | `data/relic-effect-rules.json` |
| キャンペーン | `data/campaigns.json` |
| 分隊 | `data/squads.json` |
| 等級 | `data/difficulty-grades.json` |
| 難度tier | `data/difficulty-tiers.json` |
| 現在state | `data/current-state.json` |
| MAAオペレーター名OCR | `data/recognition/maa-operator-name-ocr.json` |
| MAA ONNX/OCR asset定義 | `data/recognition/maa-onnx-ocr-assets.json` |
| MAA OCR補正ルール | `data/recognition/maa-ocr-rules.json` |
| MAA resource pipeline | `apps/rhodes-suki/resource/base/pipeline/rhodes.json` |
| 生成resource pipeline | `apps/rhodes-suki/resource/base/pipeline/rhodes-generated.json` |

`data/current-state.json` は作業状態です。コミット対象にしない前提です。

## 設計ドキュメント

| 用途 | ファイル |
| --- | --- |
| Suki UI設計思想 | `docs/suki-design-philosophy-ja.md` |
| Suki workbench設計原則 | `docs/suki-workbench-design-principles.md` |
| 情報設計 | `docs/suki-product-ui-information-architecture.md` |
| Stitch向けUIブリーフ | `docs/stitch-suki-workbench-brief.md` |
| ADB報告ガイド | `docs/debugger-adb-report-guide.md` |
| MAAFramework/SukiUI採用ADR | `docs/decisions/0001-adopt-maaframework-and-sukiui.md` |

## 現行画面の実装位置目安

各ワークスペースは `Views/Workspaces/`、右ペインは `Views/Panels/` に分割済みです。行番号は変更でずれるため、検索語として使ってください。

| 画面部分 | 検索語 |
| --- | --- |
| ラン値 | `RunFieldPreviews` |
| IS固有値 | `SpecialValuePreviews` |
| オペレーター一覧 | `FilteredOperatorRows` |
| 秘宝一覧 | `FilteredRelicRows` |
| 認識プロファイル | `ResourceProfiles` |
| 認識task一覧 | `ResourceTasks` |
| 出力部品 | `OutputParts` |
| ADBプリセット/パス/serial | `AdbPresets`, `AdbPath`, `AdbSerial` |
| ADB候補 | `AdbPathCandidates` |
| 接続端末 | `AdbDevices` |
| Runtime能力カード | `RuntimeCapabilities` |
| バグ報告ZIP | `CreateBugReportBundleCommand` |
| 右インスペクタ | `InspectorRows` |
| 最新スクショ | `LastCaptureImage` |
| 候補一覧 | `CandidateResults` |
| Resource task結果 | `ResourceTaskResults` |
| Probe | `ProbePayloads`, `ProbeResults` |
| 認識詳細JSON | `RecognitionDetailJson` |

## UI/UX改善優先度

### P0: ADB/Runtimeを段階診断にする (2026-07-04 実装済み)

`RhodesAdbDiagnosticsChecklist` + `RuntimeWorkspaceView` の「接続診断」セクションで対応済み。
次の順序で状態を表示しています。

1. ADB実行ファイルが存在する。
2. ADBが起動できる。
3. 端末が見える。
4. MAAFrameworkで接続できる。
5. スクショを1枚取得できる。
6. 解像度が 1280x720 / 16:9 系である。

各行は `未確認`, `OK`, `注意`, `失敗` の状態と、次に取る行動を1行で出します。

### P0: オペレーター/秘宝選択を軽くする

今の選択画面は、公開デバッグ前に使いにくさが目立ちます。

- 直接クリック選択に戻す。
- 選択/表示除外ボタンを外す。
- 検索、フィルター、4ペイン、仮想化を優先する。
- 長い説明や詳細は右インスペクタへ出す。
- レアリティ、職業、職分の並びを固定する。

### P1: 認識デバッグをframe単位に整理する

認識バグは、保存フレームから再現できることが最重要です。

- `frame_id`
- スクショPNG
- state snapshot
- MAA task result
- OCR/ROI detail
- candidate
- applied diff

この一連を同じUI上で辿れるようにします。

### P1: 右インスペクタを本当に使える場所にする

右インスペクタは「選択中のものの根拠」を出す場所です。

- ADB選択時: path, serial, preset, capture/input method, error原因。
- オペレーター選択時: selected状態、職業/職分、認識候補、OCR raw。
- 秘宝選択時: owned数、カテゴリ、認識候補、効果文。
- 認識候補選択時: raw OCR, ROI, normalized value, confidence, applied state。
- Debug選択時: 保存先、ZIP内容、frame/log対応。

### P2: 出力/OBSは編集UIから分離したまま整える

OBSに出す情報と内部検証情報を混ぜないでください。

## ビジュアル方針

既存docsの方針を引き継ぎます。

- 暗色ベース。
- 装飾的なヒーロー、グラデーション、カード過多は避ける。
- デバッグ向けに密度は高くてよいが、行間と固定幅は守る。
- 右ペイン、左ペインの幅は安定させる。
- 長いパス、JSON、OCR rawは折り返しまたは省略を明示する。
- ボタンは操作対象の近くに置く。
- 状態表示は色だけに頼らず、短いラベルも併用する。
- 大量リストは固定行高と仮想化を前提にする。

推奨トークン:

| 種類 | 値 |
| --- | --- |
| App background | `#0F1415` |
| Panel | `#101617` |
| Row | `#151D1E` |
| Elevated row | `#132021` |
| Border | `#2B3638` |
| Accent | muted teal/green |
| Radius | 6px to 8px |
| Font | system UI, FigmaではInter相当 |

## 別UI/UX担当への依頼文

```text
RHODES OBS COMMANDER3373 の Avalonia/SukiUI 版UIを、公開デバッグ向けの検証ワークベンチとして改善してください。

対象は ADB/MAA接続、認識再実行、オペレーター/秘宝選択、OBS出力、バグ報告です。
Electron/Tauri/旧OCR/希望/耐久/シールド/指揮Lvは対象外です。
16:9/1280x720、MAA-OCR既定、GLM/Ollama任意導入を前提にしてください。

最優先は以下です。
1. ADB/Runtimeを段階診断UIにする。
2. オペレーター/秘宝を直接クリック選択、4ペイン、軽量フィルターに戻す。
3. 認識候補、保存フレーム、state反映差分をframe_id単位で追えるようにする。
4. 右インスペクタを、選択中要素の根拠表示として使える形にする。

派手なダッシュボードではなく、配信支援とデバッグに耐える密度の高い作業UIにしてください。
```

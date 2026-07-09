# 公開デバッグ相談用メモ

## 目的

RHODES OBS COMMANDER3373 の公開デバッグ前に、より上位の設計レビューAIへ相談するための材料をまとめる。

初回公開デバッグは完成度よりも、報告者から届いたZIPだけでこちらの環境に失敗を再現できることを最優先にする。

## 現在の方針

- 配布対象は Avalonia/SukiUI のみ。
- Electron / Tauri / 旧OCR導線は公開デバッグ対象外。
- OCR既定は MAA-OCR。
- GLM-OCR / Ollama は任意導入扱い。
- 対象ISは IS#5 `サルカズの炉辺奇談` のみ。
- 基準解像度は 1280x720、16:9。
- 対象認識profileは次のみ。
  - `runStatusFull`
  - `operatorsFull`
  - `relicsFull`
  - `is5ThoughtFull`
  - `is5AgeFull`
- state/OBSへ残すラン基本値は次のみ。
  - 源石錐
  - 等級
  - 分隊
  - IS#5特殊値、思案、時代
  - オペレーター
  - 秘宝
- 捨てる項目は次。
  - 希望
  - 耐久値
  - シールド
  - 指揮Lv
  - 他IS特殊値の公開デバッグ導線

## 作業ルート

```text
O:\Arknights_Rogue_OBSTool
```

`C:\Users\owner` をプロジェクトルートとして扱わない。

## 実行時データの保存場所

実行時の保存先は `AppContext.BaseDirectory` 基準。

開発ビルドでは概ね次のような場所になる。

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\bin\Debug\net8.0\
```

portable EXEではEXE配置フォルダが `AppContext.BaseDirectory` になる。

### Debug Root

```text
<AppContext.BaseDirectory>\RHODES OBS COMMANDER3373 Debug Logs\
```

定義元:

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesSukiDebugPaths.cs
```

### Frame Records

ADB撮影ごとに、スクリーンショット、metadata、state snapshotを同じ `frameId` で保存する。

```text
<AppContext.BaseDirectory>\RHODES OBS COMMANDER3373 Debug Logs\Frame Records\
```

想定ファイル:

```text
frame-<frameId>.png
frame-<frameId>.json
frame-<frameId>-state.json
```

関連実装:

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesFrameRecordStore.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesFrameRecordHistory.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\ViewModels\MainWindowViewModel.cs
```

### Recognition Scans

MAA Resource認識結果、候補、runtime evidence、frameId、state snapshot pathなどを保存する。

```text
<AppContext.BaseDirectory>\RHODES OBS COMMANDER3373 Debug Logs\Recognition Scans\
```

想定ファイル:

```text
recognition-*.json
```

関連実装:

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesMaaRecognitionEvidenceLog.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesRecognitionScanHistory.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesRecognitionWorkflow.cs
```

### ROI Drafts / ROI Sessions

ROI調整のドラフトとセッション証跡。

```text
<AppContext.BaseDirectory>\RHODES OBS COMMANDER3373 Debug Logs\ROI Drafts\
<AppContext.BaseDirectory>\RHODES OBS COMMANDER3373 Debug Logs\ROI Sessions\
```

関連実装:

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesMaaRoiEditDraftLog.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesMaaRoiAdjustmentSessionLog.cs
```

### Bug Reports

ワンクリック生成する公開デバッグ報告ZIP。

```text
<AppContext.BaseDirectory>\RHODES OBS COMMANDER3373 Debug Logs\Bug Reports\
```

想定ファイル:

```text
RHODES-OBS-COMMANDER3373-bug-report-*.zip
```

ZIPに含めるもの:

- Debug root配下の許可拡張子ファイル
- Frame Records
- Recognition Scans
- ROI Drafts
- ROI Sessions
- `state/current-state.json`
- `state/suki-settings.json`
- 最新capture
- 最新recognition log
- `manifest.json`
- `README.txt`

ZIPから除外するもの:

- GLM-OCR本体
- Ollama本体
- モデル
- 巨大cache
- `dist`
- `outputs`
- `runtimes`
- `.dll`
- `.exe`
- `.zip`

関連実装:

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesBugReportBundle.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesBugReportImport.cs
```

受け取った報告ZIPはDebug画面から取り込み、次へ展開する。

```text
<AppContext.BaseDirectory>\RHODES OBS COMMANDER3373 Debug Logs\Bug Reports\imported\
```

取り込み後は、展開先の `Frame Records` / `Recognition Scans` を通常履歴にマージして、ADBなしで保存Frame再実行へ進める。

ZIPには現在のresource定義とSHA256も同梱する。

```text
resource/interface.json
resource/pipeline/rhodes.json
resource/pipeline/rhodes-generated.json
resource/recognition/maa-tasks.json
resource/recognition/scan-profiles.json
manifest.json の resourceHashes
```

### current-state.json

現在state。`RhodesRunCatalog.ResolveDataRoot()` と `RhodesRunCatalog.ResolveStatePath()` で探索される。

開発時の代表パス:

```text
O:\Arknights_Rogue_OBSTool\data\current-state.json
```

portable時は、配布フォルダ内の `data\current-state.json` が候補になる。

関連実装:

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesRunCatalog.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesRunStateStore.cs
```

### Suki設定

ADB設定、profile選択、UI/OBS設定など。

```text
<AppContext.BaseDirectory>\user-data\suki-settings.json
```

関連実装:

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesSukiSettingsStore.cs
```

### MAA Resource / Interface

MAAFrameworkが使うResource定義。

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\interface.json
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\resource\base\pipeline\rhodes.json
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\resource\base\pipeline\rhodes-generated.json
```

生成元データ:

```text
O:\Arknights_Rogue_OBSTool\data\recognition\maa-tasks.json
O:\Arknights_Rogue_OBSTool\data\recognition\scan-profiles.json
```

生成/検証コマンド:

```powershell
npm run maa:resource:check
npm run maa:interface:check
npm run maa:contract:check
```

## 相談したいこと

### 1. 報告ZIPだけで再現するために、まだ足りないデータは何か

現在ZIPには Debug Logs、Frame Records、Recognition Scans、ROI証跡、state、settings、manifestを入れる設計。

質問:

- `frameId`、PNG、metadata、state snapshot、recognition result、runtime evidenceだけで再現に十分か。
- ADB path、serial、preset、撮影方式、入力方式、MAA version、解像度、OS情報以外に必要な環境情報はあるか。
- `current-state.json` と `suki-settings.json` を同梱するだけで、state反映差分の再現に十分か。
- 個人情報や不要データ混入を避けるため、ZIP manifestに追加すべき説明や除外条件はあるか。

### 2. Frame Record / Replay のスキーマは破綻しないか

現在は保存PNGを `MaaTasker.AppendRecognition` に渡し、ADBなしで同じprofileを再認識する方針。

質問:

- Frame Record metadataに保存すべき最低項目は何か。
- `profileId` と `taskEntries` はmetadata側にも固定保存すべきか。
- MAA Resource定義が後日変わった場合、過去Frameの再実行結果が変わる問題をどう扱うべきか。
- ZIP再現時に「当時のResource定義」も入れるべきか、それともmanifestのcommit hashだけで足りるか。

### 3. RecognitionResult と StateDiff の境界は妥当か

理想パイプライン:

```text
IScreenSource
  -> Frame
  -> Recognizer
  -> RecognitionResult
  -> StateReducer
  -> StateDiff
  -> State / OBS output
```

質問:

- Recognizerは候補生成までに留め、state反映は完全にReducer側へ分離すべきか。
- 現在の `MaaCandidatePreview` を公開デバッグ用の安定スキーマとして扱ってよいか。
- 候補0件、候補はあるが反映なし、反映値が違う、の3系統をログ上で明確に分けるために追加すべき項目は何か。

現状の実装メモ:

- `SukiCandidateApplySummary` に候補単位の `outcomes` を持たせた。
- `Recognition Scans` の `evidence.stateApply` に `appliedCount` / `ignoredCount` / `appliedFields` / `outcomes` / `localFallbackUsed` / `apiError` を保存する。
- `outcomes` には候補index、kind、label、value、identity、`applied` / `ignored`、反映field、`ignoredReason` を残す。
- 低信頼の重複runStatus候補も `lower-confidence-duplicate` として消さずに残す。

### 4. MAAFrameworkへ委譲する範囲は正しいか

現在の線引き:

- MAAFrameworkへ委譲
  - ADB接続
  - screencap
  - 入力方式
  - TemplateMatch
  - MAA-OCR
  - `AppendTask`
  - `AppendRecognition`
- RHODES側で保持
  - IS状態モデル
  - state reducer
  - OBS出力
  - オペレーター/秘宝マスタ
  - Frame Record
  - Replay
  - Bug Report ZIP

質問:

- 公開デバッグ前にMAA Pipelineの高級機能まで寄せるべきか。
- それとも接続/撮影/認識だけMAAに委譲し、state/報告基盤はRHODES側に残すべきか。
- 将来的にMAAFrameworkファミリーのツールとして協力しやすい境界はどこか。

### 5. ADB/MAA診断で最優先に潰すべき失敗パターンは何か

対象:

- MuMu
- LDPlayer
- Google Play Games developer emulator

質問:

- `DLL not found`
- VC++ runtime不足
- ADB拒否
- offline
- 複数端末
- serial不一致
- 非1280x720系
- screencap方式不一致
- 入力方式不一致

これらをどの順番で段階診断UIに出すべきか。

現状の診断UI:

- Runtime画面の接続診断に6段階を表示する。
  - ADB実行ファイル
  - ADB起動
  - 端末検出
  - MAA接続
  - スクショ取得
  - 解像度 1280x720 / 16:9
- `診断コピー` ボタンで、ADB preset / path / serial / 撮影方式 / 入力方式 / OCR / capture / MAA状態 / 各ステップをクリップボードへ出せる。
- 関連実装:

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesAdbDiagnosticsChecklist.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Views\Workspaces\RuntimeWorkspaceView.axaml
```

### 6. 配布フォルダ構成をどう切るべきか

懸念:

- DLLなどEXE以外のファイルが多い。
- ただしMAA native runtimeを隠しすぎると `DLL not found` が再発する。
- ユーザーはインストーラー起動に抵抗があり、portable EXE/ZIP配布を希望している。

質問:

- single-file publishとMAA native runtime外置きのバランスはどうすべきか。
- `runtimes\win-x64\native` を残すべきか、アプリ側で退避/探索しやすいフォルダに寄せるべきか。
- 報告ZIPにはruntime DLLを含めないが、manifestにruntime検出結果をどこまで書くべきか。

### 7. 公開デバッグ前にOCR精度へ戻るべき条件は何か

現在は再現基盤を優先し、OCR精度チューニングは原則凍結。

質問:

- どの水準までFrame Record / Replay / ZIPができればOCR改善へ戻ってよいか。
- Sarkaz限定でも公開デバッグ前に最低限直すべきOCRバグの基準は何か。
- オペレーター/秘宝/思案/時代のうち、先にゴールデンセット化すべき対象はどれか。

### 8. UIは最低限どこまで必要か

公開デバッグ時点の画面:

- Main
  - 現在state
  - OBS出力プレビュー
  - 接続状態
- Runtime
  - ADB/MAA接続
  - 撮影診断
  - MuMu/LD/Google Play向けプリセット
- Recognition
  - Sarkaz対象profile実行
  - 候補
  - Raw OCR
  - ROI
  - state差分
- Debug
  - Frame Record読込
  - 保存Frame再認識
  - ログ確認
  - バグ報告ZIP作成
- Operator / Relic
  - 検索
  - フィルタ
  - 直接クリック選択
  - 最大4ペイン

質問:

- 初回公開デバッグで不要なUIは何か。
- オペレーター/秘宝選択UIはどこまで直せば「最低限使える」と言えるか。
- ADB診断UIはどの情報をトップに固定表示すべきか。

## 相談時に見せるべき主要ソース

```text
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesPublicDebugPolicy.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesSukiDebugPaths.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesFrameRecordStore.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesFrameRecordHistory.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesBugReportBundle.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesBugReportImport.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesAdbDiagnosticsChecklist.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesMaaSession.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesMaaResourceCatalog.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesRecognitionWorkflow.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\Services\RhodesRecognitionCandidateApplier.cs
O:\Arknights_Rogue_OBSTool\apps\rhodes-suki\ViewModels\MainWindowViewModel.cs
```

## 相談時に見せるべきテスト

```text
O:\Arknights_Rogue_OBSTool\tests\rhodes-suki\Program.cs
O:\Arknights_Rogue_OBSTool\tests\suki-maaframework-shell.test.mjs
```

確認済みコマンド:

```powershell
npm run suki:test
npm test
npm run verify:desktop
```

## 最重要の問い

公開デバッグへ出す判断基準は、次の1点に集約する。

```text
報告者のバグ報告ZIPだけで、こちらの環境に同じ失敗を再現できるか。
```

これが満たせない場合、OCR精度やUI改善を進めても公開デバッグの価値が落ちる。

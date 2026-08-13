# MAA 開発ツール連携・認識実験室 仕様

## 承認と前提

2026-08-13 のユーザー指示「MaaEvidenceKit まで高い優先度の機能を取り込む」「Node も更新してよい」「続けてください」に基づく実装仕様である。

前提は次のとおり。

1. Avalonia / SukiUI の現行基幹UIは維持し、外部UIへ置き換えない。
2. Maa Support Extension、MaaPipelineEditor、MaaLogAnalyzer、MaaEvidenceKit はローカル開発・診断ツールとして連携する。ソース一式は同梱しない。
3. MFAToolsPlus からは認識調整の考え方だけを取り入れ、コードを複製しない。
4. 歳の銭は保存画像または現在のキャプチャを対象に、現行認識器とMAA認識を比較する。結果をラン状態へ自動反映しない。
5. 認識実験室からClick、Swipe、Key、Android Backなどの操作命令は発行しない。
6. 外部送信、匿名遥測、自動更新は既定で無効にする。

## Objective

RHODES OBS COMMANDER3373 の認識開発と不具合調査を、外部MAA系ツールと3373内の認識ラボから再現可能にする。

主な利用者は本プロジェクトの開発者・検証者である。成功状態は次のとおり。

- `interface.json` とResourceをMSE/MPEで安全に調査できる。
- MAAログまたは報告ZIPをMaaLogAnalyzerへ渡せる。
- MaaEvidenceKitでローカル材料をオフライン解析し、`maa-evidence/v1`を3373で要約できる。
- 保存Frameに対してOCR、TemplateMatch、ColorMatch、現行の歳の銭認識器を同じROIで比較できる。
- 実行時間、命中、認識詳細JSON、ROIを残し、歳の銭の誤認識を調整できる。
- 認識結果は候補のままで、手動入力を権威ある状態として維持する。

## Tech Stack

- .NET 8.0
- Avalonia 12.0.5
- SukiUI 7.0.1
- Maa.Framework binding 5.10.0
- Maa.Framework runtime 5.12.3
- Node.js 24 LTS
- MaaEvidenceKit 0.3.2以上（任意のローカルCLI）

上流仕様:

- Project Interface V2: https://maafw.com/docs/3.3-ProjectInterfaceV2/
- Maa Support Extension: https://github.com/neko-para/maa-support-extension
- MaaPipelineEditor: https://github.com/kqcoxn/MaaPipelineEditor
- MaaLogAnalyzer: https://github.com/MaaXYZ/MaaLogAnalyzer
- MFAToolsPlus: https://github.com/SweetSmellFox/MFAToolsPlus
- MaaEvidenceKit: https://github.com/Windsland52/MaaEvidenceKit

## Functional Scope

### 1. 開発ツール連携

Debugワークスペースにローカル開発ツール一覧を追加する。

- Maa Support Extension
  - ローカルのVS Codeを明示引数で起動し、リポジトリを開く。
  - 起動できない場合は画面内の状態表示へ理由を出す。
  - 拡張機能の自動導入は行わない。
- MaaPipelineEditor
  - MSE経由または設定済みローカル実行ファイルを優先する。
  - `rhodes-generated.json` は生成物であることを明示し、直接編集を推奨しない。
  - 正規の編集元は `data/recognition/*.json` とする。
- MaaLogAnalyzer
  - `maa.log`、`maafw.log`、フォルダー、報告ZIPを入力候補として扱う。
  - 設定済みローカル実行ファイルを起動できる。
  - 未導入時はローカル実行ファイルの指定を求め、オンライン版へ材料を自動アップロードしない。
- MaaEvidenceKit
  - `maa-evidence`、Node 24以上、バージョンを検出する。
  - 実行時に `MAA_EVIDENCE_AUTO_UPDATE=0` と `MAA_EVIDENCE_TELEMETRY=0` を必ず注入する。
  - `inspect <materials> --format json --output <file>` を引数配列として起動し、シェル文字列連結を使わない。
  - `feedback`、`telemetry enable`、ネットワーク送信を3373から実行しない。
  - 既存の`maa-evidence/v1` JSONを読み込み、artifacts、evidence、missingEvidence、warningsを要約する。

### 2. Project Interface V2強化

- `languages`へ`ja_jp`と`en_us`を宣言する。
- 国際化対象のlabel/description/titleは`$`キーにし、生成した翻訳JSONと同期する。
- 翻訳ファイルの存在と参照キーを契約テストで検証する。
- `resource.hash`は、`resource/base`だけをMaaResourceへ読み込んだ後の`MaaResourceGetHash`相当値を使用する。
- ハッシュはResource外の生成メタデータへ保存し、ハッシュ自身が計算対象へ入る循環を避ける。
- ハッシュ不一致は警告であり、認識の実行自体を禁止しない。
- `telemetry`フィールドは追加しない。

### 3. 認識実験室

Debugワークスペースに開発者向けセクションを追加する。

入力:

- 現在のMAA/ADBキャプチャ
- Frame Recordsの保存PNG
- バグ報告ZIPから展開済みの保存PNG
- 1280x720基準ROI（x、y、width、height）

モード:

- MAA OCR
- MAA TemplateMatch
- MAA ColorMatch
- 現行の歳の銭・所持銭画像認識
- 現行の歳の銭ステータス認識

共通出力:

- 成否、命中、アルゴリズム
- 実行時間ms
- 認識詳細JSON
- OCR文字列、スコア、矩形
- 使用した画像パスとROI
- 失敗理由

MAA OCR設定:

- ROI、`expected`、`only_rec`、`threshold`

TemplateMatch設定:

- ROI、template相対パス、threshold、method

ColorMatch設定:

- ROI、method、lower/upper範囲、count、connected

歳の銭設定:

- map上の有効銭と所持銭を切り替える。
- 現行`RhodesSuiCoinImageRecognizer`と`RhodesSuiCoinStatusRecognizer`の結果を表示する。
- 実行条件と結果を認識ラボJSONへ保存し、既存の安定性評価と突き合わせられるようにする。
- 認識器の閾値や正規データへは自動適用しない。既存の明示的なROI/生成元更新導線を使う。

### 4. ログ・証跡

- 認識実験室の1回の実行をローカルJSONへ保存する。
- 保存内容は入力画像のパス、画像fingerprint、payload、結果、時間、アプリ/MAAバージョンを含む。
- 既存のBug Report ZIPへ認識実験室ログを追加する。
- 選択した標準ログ、ZIP、フォルダをMaaLogAnalyzerまたはMEKへ1引数として渡す。
- 認証情報、ADBのローカル絶対パス、ユーザー名を外部送信しない。

## Commands

```powershell
npm test
npm run maa:check
npm run suki:test
npm run suki:build
npm run maa:resource:hash:generate
npm run maa:resource:hash:check
npm run verify:desktop
```

## Project Structure

```text
apps/rhodes-suki/
  Models/                  UIへ公開する不変の設定・結果モデル
  Services/                外部ツール、Evidence、認識実験の境界
  Views/Workspaces/        Recognition / Debug UI
  interface.json           生成済みProject Interface
  interface_ja_jp.json     生成済み日本語翻訳
  interface_en_us.json     生成済み英語翻訳
data/recognition/          認識定義の正規ソース
docs/design/               本仕様と設計判断
tests/rhodes-suki/         C#サービス・契約テスト
tests/*.test.mjs           生成器・配布契約テスト
tools/                     Resource/Interface/Hash生成ツール
```

## Code Style

外部プロセスは引数と環境変数を明示する。シェル展開を使わない。

```csharp
var request = new RhodesExternalToolRequest(
    executablePath,
    ["inspect", materialsPath, "--format", "json", "--output", outputPath],
    UseShellExecute: false,
    new Dictionary<string, string>
    {
        ["MAA_EVIDENCE_AUTO_UPDATE"] = "0",
        ["MAA_EVIDENCE_TELEMETRY"] = "0",
    });
```

モデルは`sealed record`を優先し、ファイルI/O・プロセス起動・認識payload生成を分離する。UIコードビハインドへ認識ロジックを置かない。

## Testing Strategy

- 小テスト: 引数生成、パス境界、環境変数、Evidence JSON要約、ROI検証、payload生成。
- 中テスト: 一時ディレクトリを使うInterface翻訳契約、認識ログ保存、外部プロセスの引数境界。
- 統合テスト: 実際のMaaResourceでhash取得、保存Frameに対する認識実行。
- UI契約: XAMLにラベル、説明、無効状態、コマンドbindingが存在すること。
- 回帰: Android Back、Click、Swipe、Keyを認識実験室payloadと外部ツール起動契約が含まないこと。

新しい振る舞いは失敗テストを先に追加してから実装する。

## Boundaries

### Always

- 1280x720 / 16:9の座標を検証する。
- 保存Frameを優先して再現可能にする。
- 外部プロセスへ引数配列を渡す。
- MEK実行時は自動更新・遥測をOFFにする。
- 正規データ更新は差分確認と明示操作を要求する。
- 既存の未コミット作業を保持する。

### Ask first

- 外部ツール本体を配布ZIPへ同梱する。
- 新しいNuGet/npm依存をプロジェクトへ追加する。
- 認識結果をラン状態へ自動適用する。
- 外部サービスへログ、画像、証跡をアップロードする。
- Maa.Framework binding/runtimeのバージョンを変更する。

### Never

- MaaAssistantArknightsまたはMFAToolsPlusのコードやアセットを複製する。
- 認識実験室からゲーム操作を実行する。
- Android Back keyeventを実装する。
- `rhodes-generated.json`を認識定義の正規編集元にする。
- 秘密情報、token、ローカル個人情報をcommitまたは外部送信する。
- MEKの`feedback`やtelemetryを3373から有効化する。

## Tasks

- [x] Project Interface V2の翻訳・hash契約を追加する。
  - Acceptance: languages、翻訳キー、hashが生成・検証される。
  - Verify: `npm run maa:check`。
  - Files: generator、contract、interface、translation、hash tool。
- [x] 開発ツールカタログと安全な外部プロセス境界を追加する。
  - Acceptance: MSE/MPE/MLA/MEKを検出し、安全な引数で開ける。
  - Verify: C#小テストとDebug UI契約。
  - Files: model/service、Debug XAML、ViewModel、tests。
- [x] MaaEvidenceKitの実行・結果取込を追加する。
  - Acceptance: offline envが強制され、`maa-evidence/v1`を要約できる。
  - Verify: 偽プロセス境界テストとJSON fixtureテスト。
  - Files: model/service、Debug XAML、ViewModel、tests。
- [x] 認識ラボのpayload生成と証跡保存を追加する。
  - Acceptance: OCR/TemplateMatch/ColorMatch/銭認識の入力を検証し、結果・時間・ROIを保存する。
  - Verify: C#小/中テスト。
  - Files: model/service、Recognition XAML、ViewModel、tests。
- [x] 統合検証を行う。
  - Acceptance: JS/C#/contract/buildが成功し、禁止命令がない。
  - Verify: `npm run verify:desktop`とdiffレビュー。

## Verification Evidence

- `npm test`: 427件成功、失敗0件。
- `npm run maa:check`: Resource、hash、Interface V2、contractの全検証成功。Resource hashは`20eca4457ab951f2`。
- `npm run verify:desktop`: C#サービス試験284件成功、Avaloniaビルド警告0・エラー0。
- MaaEvidenceKit 0.3.2 smoke: `maa-evidence/v1`、artifact 5件、evidence 186件、missing 1件、warning 1件。
- 実行コードとユーザー向け説明にAndroid Back / `KEYCODE_BACK` / `keyevent 4`なし。認識ラボpayloadにAction、Click、Swipe、Keyなし。

## Success Criteria

- Node 24上で既存テストが退行しない。
- MEK 0.3.2以上をオフライン設定で検出・実行できる。
- `maa-evidence/v1`の主要6セクションを欠落と警告を区別して表示できる。
- PI V2のlanguagesとresource.hashが公式形式に一致する。
- MSE/MPE/MLAへ正しい現物パスを渡せる。
- 歳の銭の保存Frameで複数認識方式を同一ROIから比較できる。
- 認識実験室の実行はラン状態を変更せず、操作命令を発行しない。
- 全自動テストとデスクトップビルドが成功する。

## Open Questions

- 公開デバッグZIPへ外部ツール本体は同梱しない。将来同梱が必要になった時点で、容量・ライセンス・更新責任を再評価する。
- Maa.Framework binding 5.10.0とruntime 5.12.3の整合更新は本仕様の範囲外とし、別タスクで行う。
- 歳の銭の閾値を正規データへ反映する判断は、保存Frame corpusで誤検出ゼロ条件を満たした後に行う。

# Codex回答: 有効銭・保有銭 状態取得の安定化計画 (2026-07-24取得)

ブリーフ: docs/codex-coin-stability-brief-2026-07-09.md への回答。セッション 019f0d1d-8003-7fa1-8b80-87cabec439b3 より抽出。

---

**結論**
ブリーフの方向性は妥当ですが、現行コードは作成時点より進んでいます。特にAは部分実装済みです。最優先はAの新規追加ではなく、現在並存している「スロット画像認識」「OCR行連結」「状態認識」「候補統合」を、単一のスロット観測モデルへ統合することです。

## 1. A〜Fへの技術的指摘

**A. スロット幾何 + 状態テンプレート**

- [RhodesSuiCoinImageRecognizer.cs](O:/Arknights_Rogue_OBSTool/apps/rhodes-suki/Services/RhodesSuiCoinImageRecognizer.cs) には、有効銭3枠・保有銭9枠の固定スロット幾何がすでにあります。
- [RhodesSuiCoinStatusRecognizer.cs](O:/Arknights_Rogue_OBSTool/apps/rhodes-suki/Services/RhodesSuiCoinStatusRecognizer.cs) にも、10種の状態テンプレート、状態位置推定、色・形状比較、背景との差分判定が実装済みです。
- したがって、`maa-tasks.json`へ状態10種の静的タスクを追加するだけでは二重実装になります。
- 必要なのは、銭名・銭画像・状態を同じ`frameId + passIndex + slotIndex`へ集約する共通観測モデルです。
- 有効銭はスロットOCRが成功すると画像結果を捨てる経路があり、状態だけは依然としてY座標連結に依存します。ここがAの主要な未完部分です。
- MAA TemplateMatchを正式な主経路にするなら、現在のSkiaローカル照合との責務を明確にし、二重判定を残さない方がよいです。

**B. `coinId`統合と合議**

- 現在は`coinId + statusId`で候補が分裂します。
- ただし単純な`coinId`統合も危険です。同じ銭を複数枚持ち、それぞれ状態が異なるケースを潰します。
- 統合単位は、最初にスロット観測、次にスクロール追跡された銭インスタンス、最後にstate用マルチセットとすべきです。
- `ReconcileOwnedCoinStatusObservations`は多数決ではありません。最大表示枚数を決めた後、高confidenceの状態付き観測を優先して詰める処理です。単発の誤状態も残り得ます。
- 「状態なし」と「状態不明」を分離する必要があります。両方を空文字にすると合議できません。
- 同じフレームの画像認識とOCRを2票として数えず、1スロット1票に正規化します。

**C. 空状態で既知状態を消さない**

- 現在の一括反映は`coinId + statusId`でグループ化した配列を作り直し、対象フィールドを置換します。
- 単体マージでも、状態なしが別エントリとして追加されます。ブリーフの保護規則は未実装です。
- 既存stateは手動入力を含む権威データなので、状態不明の認識結果で上書きしてはいけません。
- 同名銭が複数ある場合、どの個体の状態を保持するか曖昧です。自動上書きではなく、認識提案とstate差分を出して明示反映する設計が必要です。

**D. パネル開閉判定**

- `is6ActiveCoinsFull`は同じ座標をopen/restoreでタップする固定トグルです。位相ずれの指摘は正しいです。
- `targetScreenId`も`run-home`のため、既存画面分類だけでは開閉を区別できません。パネル固有アンカーが必要です。
- `is6CoinsFull`は`sui-coin-list`を判定に使えますが、restore前にも閉状態確認が必要です。
- open/restoreを冪等化し、「閉なら開く」「開なら閉じる」にします。

**E. 詳細タップ**

- 最後の救済策として有効ですが、全件実行すると現在の重さを再発させます。
- 対象は`銭あり + coinId未確定`または`状態バッジあり + statusId未確定`のスロットだけに限定します。
- 1 viewportあたり最大2〜3件、詳細確認後は同一ボタンまたは画面内閉じる領域で復帰します。
- 横スクロール位置を維持し、詳細確認はスクロール前に完了させる必要があります。
- タップはスロット矩形内ランダム、Android Backは禁止です。

**F. Phase 0**

- 実装前に行う判断は正しいです。
- ただし現在確認できるFrame Recordは保有銭32枚、有効銭3枚程度で、有効銭と状態10種の評価には不足しています。
- 精度だけでなく、処理時間、候補分裂数、フレーム間の再現性も計測対象にします。

## 2. Phase 0の実行手順

現時点では一括計測用CLIがないため、計画承認後に専用プローブを追加します。

**使用フレーム**

- `outputs/suki-portable/**/Frame Records`の`is6CoinsFull`全32枚
- 同フォルダの`is6ActiveCoinsFull`全3枚
- `outputs/suki-portable/**/Bug Reports/imported`内のIS#6銭フレーム
- 追加採取: 状態なし、状態10種、同名複数枚、同名で異なる状態、スクロール中ブレ、左右端、有効銭1〜3枚

**ゴールデン定義**

`tests/fixtures/sui-coins/manifest.json`に以下をスロット単位で記録します。

```json
{
  "frameId": "...",
  "profileId": "is6CoinsFull",
  "passIndex": 0,
  "slots": [
    {
      "slotIndex": 0,
      "present": true,
      "coinId": "is6_...",
      "status": { "kind": "known", "statusId": "..." }
    }
  ]
}
```

`status.kind`は`known`、`none`、`unknown`を区別します。

**予定コマンド**

```powershell
dotnet run --project tools/rhodes-coin-stability/RhodesCoinStability.csproj -- `
  --manifest tests/fixtures/sui-coins/manifest.json `
  --frames "outputs/suki-portable" `
  --out "outputs/coin-stability/baseline"
```

同じ入力を3回実行し、結果ハッシュが一致することも確認します。

```powershell
npm run suki:test
npm test
```

**出力**

- `observations.jsonl`: フレーム・スロットごとの全根拠
- `errors.csv`: 人間が確認する誤り一覧
- `summary.json`: 集計値
- `candidate-diff.json`: 現行候補と期待マルチセットの差分
- `run-metadata.json`: commit、MAA版、テンプレートとマスタのSHA-256、処理時間

誤り分類は`coin_name_error`、`status_false_positive`、`status_false_negative`、`status_wrong_class`、`association_error`、`candidate_split`、`duplicate_count_error`、`panel_phase_error`、`slot_missed`とします。

## 3. A〜Eのファイル単位計画

**共通基盤**

- 新規 `apps/rhodes-suki/Services/RhodesSuiCoinObservation.cs`
- 新規 `apps/rhodes-suki/Services/RhodesSuiCoinObservationAggregator.cs`
- スロット、銭名、状態、各score、runner-up、source、frame/passを保持します。

**A**

- `RhodesSuiCoinImageRecognizer.cs`: スロット幾何を共通公開し、候補ではなく観測を返す。
- `RhodesSuiCoinStatusRecognizer.cs`: `known/none/unknown`と根拠値を返す。
- `RhodesMaaLocalCandidateConverter.cs`: 有効銭のY座標状態連結と画像結果排他を撤去。
- `data/recognition/maa-tasks.json`: パネルアンカーのみ追加。状態10種はPhase 0比較後に採否決定。
- `tests/fixtures/sui-coins/status-crops/`: 実機1280×720から状態バッジを切り出す。余白、縮尺、色空間を固定。

**B**

- `RhodesSuiCoinObservationAggregator.cs`: viewport内スロット集約、スクロール間追跡、独立観測合議、同名複数枚のマルチセット化。
- `RhodesMaaLocalCandidateConverter.cs`: `coinId + statusId`による早期分裂を廃止。
- `tests/rhodes-suki/Program.cs`: 同名複数、異状態、同票、低信頼、重複フレームのテスト追加。

**C**

- `RhodesRecognitionCandidateApplier.cs`: 認識提案と既存stateの状態保持規則を追加。
- `tests/rhodes-suki/Program.cs`: 既知状態をunknownで消さない、明示的変更、複数枚の個体不明時は自動変更しないテスト。

**D**

- `data/recognition/maa-tasks.json`: 有効銭パネル開状態、保有銭一覧開状態、詳細ポップアップのアンカー追加。
- `data/recognition/scan-profiles.json`: 固定トグルを状態判定付きワークフローへ移行するメタデータ追加。
- `RhodesRecognitionNavigation.cs`と`MainWindowViewModel.cs`: 条件付きopen/restore、前後フレーム記録、位相エラー出力。
- 契約テストでBack不使用とランダム矩形タップを確認。

**E**

- `RhodesSuiCoinImageRecognizer.cs`: 未確定スロット一覧と安全なタップ矩形を返す。
- `MainWindowViewModel.cs`: 件数上限付き詳細フォールバック、復帰確認、横位置維持。
- `maa-tasks.json`: 詳細画面の銭名・状態・閉じるアンカー。
- `scan-profiles.json`: 詳細表示待機時間と最大確認数。
- テストで全件巡回しないこと、強い合議結果を弱い詳細OCRで上書きしないことを確認。

今回はコード、データ、作業ツリーを一切変更していません。
# IS#6 銭安定性ゴールデンデータ

`manifest.json` は、保存済みの `is6ActiveCoinsFull` / `is6CoinsFull`
Frame Recordに対する人手確認結果です。認識器が過去に保存した候補は正解ラベルとして
使用しません。

## status

- `known`: 状態があり、`statusId`まで人手確認済み
- `none`: 状態が付いていないことを人手確認済み
- `unknown`: 状態の有無または種類を未確認。状態精度の分母から除外

`present: false` は空スロットを表します。未確認のスロットは`slots`へ追加しません。
Frameを追加する際は、画像を目視してから`frameId`、`profileId`、`passIndex`、
スロット単位の正解を記録してください。

## 実行

```powershell
dotnet run --project tools/rhodes-coin-stability/RhodesCoinStability.csproj -- `
  --manifest tests/fixtures/sui-coins/manifest.json `
  --frames "outputs/suki-portable" `
  --out "outputs/coin-stability/baseline"
```

`--frames` は複数指定できます。ディレクトリ内の通常Frame Recordに加え、
バグ報告ZIP内のFrame Recordも探索します。

# IS#6銭名 OCRゴールデンコーパス

保有銭一覧の文字認識を、実機ADBなしで比較するための合成データです。
`data/selectable-effects.json` にあるIS#6の全銭名を、Noto Sans JP Regularで描画します。

```text
npm run ocr:corpus:sui-coins
```

既定の生成先は `.agent-work/ocr-corpus/is6-sui-coins` です。
生成物は検証専用で、アプリや公開ZIPには同梱しません。

- `manifest.json`: 正解名、表示文字、フォント、文字サイズ、濃淡、ROI、画像パス
- `targets.jsonl`: 1サンプル1行のOCR評価入力
- `lines/`: 220x32の個別文字画像
- `sheets/`: 1280x720の一括評価画像

既定では14、15、16、17、18、20pxと、通常・非アクティブ・低コントラスト・軽いぼけを組み合わせます。
実行時のMAA-OCRモデルへ学習データとして投入するものではなく、ROI、拡大率、二値化、OCR補正辞書の比較に使います。

# Recognition Notes

MaaAssistantArknights can be used as a public technical reference for general ADB/OCR architecture and recognition strategy.
Do not copy its source code or assets into this project.

Initial recognition goals:

- capture screenshots through ADB
- identify screens relevant to Integrated Strategies run state
- propose relic/operator/squad updates
- assign confidence levels to suggested updates
- let manual input accept, reject, or correct suggestions

Recognition is intentionally lower priority than the manual and tournament workflows.

## MAA OCR reference

OCR/ADB半自動取得の詳細調査は [maa-ocr-research.md](./maa-ocr-research.md) にまとめる。MAAのROI/OcrDetect/ocrReplace/NumberOcrReplaceの考え方を、本プロジェクトのOCRタスクJSON設計へ反映する。

## ADB navigation entry points

1280x720を基準に、ADBスキャンは現在の統合戦略画面から以下を開いて読む。
座標はMAA式に実解像度へスケールする。初期値は実機で調整する。

- 分隊/等級: 左下の分隊情報をクリックすると、分隊と等級が同じ画面に表示される。
- 秘宝: 右側の秘宝表示をクリックして秘宝一覧を開く。
- 思案: 思案表示をクリックして思案一覧を開く。
- 構想: 思案の右側に表示される個数を `run.idea` ROIで読む。
- オペレーター: 隊員表示をクリックして招集済みオペレーター一覧を開く。

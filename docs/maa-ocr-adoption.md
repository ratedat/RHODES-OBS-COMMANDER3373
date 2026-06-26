# MAA OCR adoption

The OCR layer is intentionally based on MaaAssistantArknights rather than a zero-design OCR pipeline.

## What is reused

- MAA task JSON structure concepts: `roi`, `text`, `fullMatch`, `ocrReplace`, `replaceFull`, `withoutDet`, `isAscii`, `binThreshold`, and `useRaw`.
- MAA `NumberOcrReplace` rules for numeric OCR correction.
- Roguelike task definitions as reference data for squad/recruit/special-value screen recognition.

Vendored reference files are under `third_party/maa`. The generated local rules are in `data/recognition/maa-ocr-rules.json`.

## What remains local

- The Electron UI, HTTP API, candidate-review workflow, and overlay state model remain RHODES OBS COMMANDER3373 code.
- OCR engine invocation is a local adapter: PaddleOCR first in `auto` mode, Windows OCR fallback when PaddleOCR is unavailable.
- Recognition output is still candidate-based; OCR does not directly mutate overlay state.

## Sync flow

Run this after updating vendored MAA task files:

```powershell
node tools/sync-maa-ocr-rules.mjs
```

This regenerates `data/recognition/maa-ocr-rules.json` from the vendored MAA task JSON.

## License notes

MaaAssistantArknights is AGPL-3.0-only. RHODES OBS COMMANDER3373 is also AGPL-3.0-only, so this reuse is license-compatible. Keep `THIRD_PARTY_NOTICES.md` and `third_party/maa/LICENSE` with the source tree.

Source: https://github.com/MaaAssistantArknights/MaaAssistantArknights

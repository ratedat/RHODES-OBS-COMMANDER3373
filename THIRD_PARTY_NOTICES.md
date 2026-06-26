# Third-Party Notices

RHODES OBS COMMANDER3373 is licensed under AGPL-3.0-only. See `LICENSE` for the full license text.

## MaaAssistantArknights

This project references and adapts ADB/OCR automation design, OCR task structures, and selected OCR replacement rules from MaaAssistantArknights by Maa Team. MaaAssistantArknights is licensed under AGPL-3.0-only.

- Project: https://github.com/MaaAssistantArknights/MaaAssistantArknights
- License: AGPL-3.0-only

When code, data structures, or implementation details are copied or adapted from MaaAssistantArknights, keep source attribution near the adapted files and preserve applicable license notices.

MaaAssistantArknights logos, trademarks, and brand assets are not imported into this project.

Vendored reference files are kept under `third_party/maa`; generated OCR rule data is stored under `data/recognition/maa-ocr-rules.json`.

MAA OCR configs, dictionaries, and optional ONNX model files may be synchronized into `third_party/maa/resource` by `npm run ocr:sync-maa` or `npm run ocr:sync-maa:models`. Keep these files under the same AGPL-3.0-only attribution.

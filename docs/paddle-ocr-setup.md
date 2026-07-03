# PaddleOCR Legacy Notes

PaddleOCR and Windows OCR are no longer active recognition routes for RHODES OBS COMMANDER3373.

The supported OCR policy is:

- `profile`: use each scan profile default. Current profiles route to `maa-ocr`.
- `maa-ocr`: use MAAFramework Resource tasks and bundled MAA OCR assets.
- `glm-ocr`: optional local verification engine.

Old values such as `hybrid`, `paddle`, `windows`, `windows-paddle`, and `windows-glm` are normalized away by the app and are not shown in the UI. The former PaddleOCR, Windows OCR, and direct Python ONNX OCR adapters have been removed from the runtime tree; only MAAFramework/MAA-OCR and optional GLM-OCR should receive new work.

Use [MAA OCR adoption](maa-ocr-adoption.md) for the active MAAFramework OCR policy and [GLM-OCR optional verification setup](glm-ocr-setup.md) for local GLM verification.

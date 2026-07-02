# PaddleOCR Legacy Notes

PaddleOCR and Windows OCR are no longer active recognition routes for RHODES OBS COMMANDER3373.

The supported OCR policy is:

- `profile`: use each scan profile default. Current profiles route to `maa-ocr`.
- `maa-ocr`: use the MAA-compatible YoStarJP ONNX recognizer through ONNXRuntime.
- `glm-ocr`: optional local verification engine.

Old values such as `hybrid`, `paddle`, `windows`, `windows-paddle`, and `windows-glm` are normalized away by the app and are not shown in the UI. The former PaddleOCR adapter and bridge have been removed from the runtime tree; only MAA-OCR and optional GLM-OCR should receive new work.

Use [ONNXRuntime / FastDeploy OCR setup](onnxruntime-fastdeploy-setup.md) for the active MAA-OCR runtime and [GLM-OCR optional verification setup](glm-ocr-setup.md) for local GLM verification.

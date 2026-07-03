# ONNXRuntime / FastDeploy OCR setup

RHODES OBS COMMANDER3373 uses the MAA-compatible ONNXRuntime recognizer as the active MAA-OCR path. GLM-OCR remains an optional verification engine.

## Windows app policy

FastDeploy itself is **not** a required runtime dependency for RHODES OBS COMMANDER3373. The current upstream FastDeploy v2 line is Linux-focused and is now positioned primarily as a PaddlePaddle LLM/VLM deployment toolkit. For the Windows Suki/Avalonia app, the safer path is to reuse MAA-compatible OCR assets (`inference.onnx` + `keys.txt`) and execute them directly through ONNXRuntime.

If we later need full PPOCR detection/parsing behavior, treat FastDeploy as a separate optional research track instead of mixing it into the default OCR venv.

## Why this is separate

MaaAssistantArknights loads its OCR assets as `inference.onnx` plus `keys.txt` through FastDeploy PPOCRv3, with ONNXRuntime selected as the backend. RHODES uses those MAA-compatible OCR assets directly for fixed-ROI recognition:

1. Sync MAA OCR configs, dictionaries, and optional ONNX model files.
2. Verify the local Python OCR runtime has `onnxruntime`, `numpy`, and `Pillow`.
3. Route active scan profiles through `maa-ocr`; use `glm-ocr` only when explicitly selected for verification.

## Install runtime dependencies

```powershell
Set-Location -LiteralPath 'O:\Arknights_Rogue_OBSTool'
py -3.12 -m venv .venv-ocr
.\.venv-ocr\Scripts\python.exe -m pip install --upgrade pip
.\.venv-ocr\Scripts\python.exe -m pip install -r requirements-ocr.txt
$env:RHODES_PYTHON = 'O:\Arknights_Rogue_OBSTool\.venv-ocr\Scripts\python.exe'
npm run ocr:probe
```

`requirements-ocr.txt` includes `onnxruntime`, `numpy`, and `Pillow` for the MAA-OCR bridge. Legacy PaddleOCR packages may still be present in older environments, but they are not part of the active OCR route.

## Sync MAA OCR assets

Lightweight configs, dictionaries, and version files:

```powershell
npm run ocr:sync-maa
```

Optional ONNX model files as well:

```powershell
npm run ocr:sync-maa:models
```

The optional model files are ignored by Git via `third_party/maa/**/inference.onnx`. They are local runtime assets and can be redownloaded from MAA when needed.

## Files used

- `third_party/maa/resource/ocr_config.json`
- `third_party/maa/resource/global/YoStarJP/resource/ocr_config.json`
- `third_party/maa/resource/PaddleOCR/rec/keys.txt`
- `third_party/maa/resource/global/YoStarJP/resource/PaddleOCR/rec/keys.txt`
- Optional ONNX files under `third_party/maa/**/inference.onnx`
- Generated manifest: `data/recognition/maa-onnx-ocr-assets.json`


## Use the MAA ONNX recognizer

The recognizer is available as the explicit `maa-ocr` engine:

```powershell
$env:RHODES_OCR_ENGINE = 'maa-ocr'
$env:RHODES_PYTHON = 'O:\Arknights_Rogue_OBSTool\.venv-ocr\Scripts\python.exe'
npm run suki:run
```

Aliases accepted by the app are `maa-onnx`, `maa`, and `onnx`; they normalize to `maa-ocr`.

This path currently uses the MAA YoStarJP recognition ONNX model and `keys.txt` for fixed ROI recognition only. It does not yet run the MAA detector model or FastDeploy PPOCR pipeline. The active MAAFramework migration targets originium ingots, difficulty, squad, IS-specific values, operators, and relics. Hope, life, shield, and command level are not recognition targets.

Optional environment variables:

- `RHODES_MAA_ONNX_LOCALE`: `jp` or `common`. Default `jp`.
- `RHODES_MAA_ONNX_REC_MODEL`: override recognizer ONNX model path.
- `RHODES_MAA_ONNX_REC_KEYS`: override recognizer dictionary path.
- `RHODES_MAA_OCR_CONFIG`: override MAA OCR equivalence config path.
- `RHODES_MAA_ONNX_PREPROCESS`: `rgb`, `gray`, or `invert`. Default `rgb`.
- `RHODES_MAA_ONNX_REC_HEIGHT`: recognizer input height. Default `48`.
- `RHODES_MAA_ONNX_WIDTH_MULTIPLE`: width rounding multiple. Default `32`.


## Runtime checks

```powershell
npm run ocr:probe
npm run ocr:probe:strict
```

`ocr:probe:strict` exits non-zero when the Python runtime cannot import `onnxruntime`, `numpy`, and `Pillow`. `fastdeploy` is reported when present, but it is not required for the current setup because the first ONNX slice will use ONNXRuntime directly from Python.

## Sources

- ONNX Runtime Python API: https://github.com/microsoft/onnxruntime/blob/main/docs/python/api_summary.rst
- PaddlePaddle FastDeploy repository: https://github.com/PaddlePaddle/FastDeploy
- ONNX Runtime Node.js binding: https://github.com/microsoft/onnxruntime/blob/main/js/node/README.md
- MAA FastDeploy OCR loader: https://github.com/MaaAssistantArknights/MaaAssistantArknights/blob/dev-v2/src/MaaCore/Config/Miscellaneous/OcrPack.cpp
- MAA OCR equivalence config: https://github.com/MaaAssistantArknights/MaaAssistantArknights/blob/dev-v2/resource/global/YoStarJP/resource/ocr_config.json

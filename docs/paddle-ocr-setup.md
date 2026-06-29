# PaddleOCR optional setup

RHODES OBS COMMANDER3373 can use PaddleOCR for ADB screenshot OCR and falls back to Windows OCR when PaddleOCR is unavailable. The app auto-detects project `.venv-ocr`, `%USERPROFILE%\.paddleocr-mcp-venv\Scripts\python.exe`, and `%USERPROFILE%\.venv-ocr\Scripts\python.exe` before falling back to `python`.

## Why PaddleOCR

MAA-style recognition works best when OCR is run on fixed ROIs and then normalized. PaddleOCR is a better fit than Windows OCR for small UI numerals such as life points, shield, and command level.

## Install in a local virtual environment

```powershell
Set-Location -LiteralPath 'O:\Arknights_Rogue_OBSTool'
py -3.11 -m venv .venv-ocr
.\.venv-ocr\Scripts\python.exe -m pip install --upgrade pip
.\.venv-ocr\Scripts\python.exe -m pip install -r requirements-ocr.txt
```

## Use PaddleOCR from the app

```powershell
# RHODES_PYTHON is optional when PaddleOCR is installed in %USERPROFILE%\.paddleocr-mcp-venv or .venv-ocr.
$env:RHODES_OCR_ENGINE = 'paddle'
$env:RHODES_PADDLE_DEVICE = 'cpu'
npm run app:debug
```

Use `RHODES_PYTHON` only when PaddleOCR is installed somewhere else:

```powershell
$env:RHODES_PYTHON = 'O:\Arknights_Rogue_OBSTool\.venv-ocr\Scripts\python.exe'
```

`RHODES_OCR_ENGINE` values:

- `auto`: try PaddleOCR, then Windows OCR fallback.
- `paddle`: require PaddleOCR and report OCR setup errors.
- `windows`: use Windows.Media.Ocr only.
- `maa-onnx`: use the MAA YoStarJP ONNX recognizer through ONNXRuntime. This is an explicit experimental engine for fixed ROI recognition.
- `hybrid`: merge MAA ONNX and PaddleOCR fixed-ROI results into one candidate frame. Prefer this for local experiments.
- `windows-glm`: merge Windows OCR and optional GLM-OCR fixed-ROI results. This is verification-only and requires the setup in [GLM-OCR optional verification setup](glm-ocr-setup.md).
- `glm-ocr`: require GLM-OCR directly. This is verification-only and should not be used as the general default.

Optional Paddle settings:

- `RHODES_PADDLE_DEVICE`: pass a PaddleOCR device option, for example `cpu`.
- `RHODES_PADDLE_RECOGNITION_ONLY`: default `1`. Uses fixed ROI crops with PaddleOCR `TextRecognition`, avoiding the slower/failing text detection pipeline on Windows CPU.
- `RHODES_PADDLE_REC_MODEL`: recognition model name. Default `PP-OCRv6_medium_rec`, which reads the current status ROIs well.
- `RHODES_PADDLE_LANG`: pass a PaddleOCR language option when the full OCR pipeline is used.
- `RHODES_PADDLE_OCR_VERSION`: pass a PaddleOCR model version option when the full OCR pipeline is used.

## Current status ROI

The squad-select status band uses fixed 1280x720 base ROIs and scales to the actual screenshot size, matching MAA-style task data. The relevant fields are:

- `run.life_points`: current life value before slash.
- `run.shield`: shield value beside the shield icon.
- `run.command_level`: command level numeral.
- `run.squad_name`: selected squad name.
- `run.difficulty_grade`: selected difficulty grade numeral.
- `run.status_band`: fallback OCR band for Windows OCR.

OCR candidates still go to review first and do not directly mutate the overlay state.

## ONNXRuntime path

For MAA ONNXRuntime/FastDeploy-compatible assets, see [ONNXRuntime / FastDeploy OCR setup](onnxruntime-fastdeploy-setup.md). The current app still defaults to PaddleOCR fixed-ROI recognition until the ONNX recognizer is promoted from setup to active OCR engine.

## Sources

- PaddleOCR installation docs: https://www.paddleocr.ai/latest/en/version3.x/installation.html
- PaddleOCR OCR pipeline Python API docs: https://www.paddleocr.ai/latest/en/version3.x/pipeline_usage/OCR.html

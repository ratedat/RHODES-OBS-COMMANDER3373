# PaddleOCR legacy notes

PaddleOCR and Windows OCR are no longer active recognition routes for RHODES OBS COMMANDER3373. The active OCR policy is **MAA-OCR by profile default, with GLM-OCR as an optional verification engine**. This file is retained only for historical setup notes while the old adapters remain in the tree.

## Why PaddleOCR

MAA-style recognition works best when OCR is run on fixed ROIs and then normalized. The active MAAFramework direction keeps base recognition to originium ingots, difficulty, squad, and campaign-specific values. Hope, life points, shield, and command level are no longer recognition targets.

## Install in a local virtual environment

```powershell
Set-Location -LiteralPath 'O:\Arknights_Rogue_OBSTool'
py -3.11 -m venv .venv-ocr
.\.venv-ocr\Scripts\python.exe -m pip install --upgrade pip
.\.venv-ocr\Scripts\python.exe -m pip install -r requirements-ocr.txt
```

## Legacy manual PaddleOCR run

```powershell
# This route is no longer exposed in the app UI.
$env:RHODES_OCR_ENGINE = 'maa-ocr'
$env:RHODES_PADDLE_DEVICE = 'cpu'
npm run app:debug
```

Use `RHODES_PYTHON` only when PaddleOCR is installed somewhere else:

```powershell
$env:RHODES_PYTHON = 'O:\Arknights_Rogue_OBSTool\.venv-ocr\Scripts\python.exe'
```

Active `RHODES_OCR_ENGINE` values:

- `profile`: use each scan profile's default. Current profiles route to `maa-ocr`.
- `maa-ocr`: use the MAA YoStarJP ONNX recognizer through ONNXRuntime.
- `glm-ocr`: require GLM-OCR directly. This is optional verification and should not be the general user default.

Old values such as `hybrid`, `paddle`, `windows`, `windows-paddle`, and `windows-glm` are normalized away by the app and are not shown in the UI.

Optional Paddle settings:

- `RHODES_PADDLE_DEVICE`: pass a PaddleOCR device option, for example `cpu`.
- `RHODES_PADDLE_RECOGNITION_ONLY`: default `1`. Uses fixed ROI crops with PaddleOCR `TextRecognition`, avoiding the slower/failing text detection pipeline on Windows CPU.
- `RHODES_PADDLE_REC_MODEL`: recognition model name. Default `PP-OCRv6_medium_rec`, which reads the current status ROIs well.
- `RHODES_PADDLE_LANG`: pass a PaddleOCR language option when the full OCR pipeline is used.
- `RHODES_PADDLE_OCR_VERSION`: pass a PaddleOCR model version option when the full OCR pipeline is used.

## Current status ROI

The squad-select status band uses fixed 1280x720 base ROIs and scales to the actual screenshot size, matching MAA-style task data. The active fields are:

- `run.ingot`: originium ingots.
- `run.idea.current`: IS#5 conception value.
- `run.squad_name`: selected squad name.
- `run.difficulty_grade`: selected difficulty grade numeral.

OCR candidates still go to review first and do not directly mutate the overlay state.

## ONNXRuntime path

For MAA ONNXRuntime/FastDeploy-compatible assets, see [ONNXRuntime / FastDeploy OCR setup](onnxruntime-fastdeploy-setup.md). The current app defaults to MAA-OCR for active scan profiles.

## Sources

- PaddleOCR installation docs: https://www.paddleocr.ai/latest/en/version3.x/installation.html
- PaddleOCR OCR pipeline Python API docs: https://www.paddleocr.ai/latest/en/version3.x/pipeline_usage/OCR.html

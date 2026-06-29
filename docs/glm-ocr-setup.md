# GLM-OCR optional verification setup

GLM-OCR support is experimental and opt-in. RHODES OBS COMMANDER3373 does not bundle GLM-OCR, Python packages, or model files. Use it only for local verification when Windows OCR / MAA ONNX / PaddleOCR misses short operator names.

## OCR engine values

Set the engine from the app's `ADB / OCR接続` panel:

- `プロファイル既定`: existing profile routing. This is the default and keeps normal users on the current OCR path.
- `Windows + GLM-OCR 検証`: runs Windows OCR first and merges GLM-OCR fixed-ROI output when available. This is the recommended verification mode.
- `GLM-OCR 検証`: requires GLM-OCR and uses it directly. Use only when comparing raw GLM-OCR behavior.

The same values can be forced by environment variable:

```powershell
$env:RHODES_OCR_ENGINE = 'windows-glm'
```

## Local SDK mode

Create a separate optional environment. Do not add these packages to the app runtime environment for general users.

```powershell
py -3.12 -m venv .venv-glm-ocr
.\.venv-glm-ocr\Scripts\python.exe -m pip install --upgrade pip
.\.venv-glm-ocr\Scripts\python.exe -m pip install -r requirements-glm-ocr.txt
$env:RHODES_PYTHON = 'O:\Arknights_Rogue_OBSTool\.venv-glm-ocr\Scripts\python.exe'
$env:RHODES_OCR_ENGINE = 'windows-glm'
```

The bridge imports `glmocr` and calls its Python `parse()` API for each fixed ROI crop.

## Local server mode

GLM-OCR also documents a Flask service. Start it separately, then point RHODES at the local endpoint:

```powershell
.\.venv-glm-ocr\Scripts\python.exe -m glmocr.server
$env:RHODES_GLM_OCR_MODE = 'server'
$env:RHODES_GLM_OCR_ENDPOINT = 'http://127.0.0.1:5002/glmocr/parse'
$env:RHODES_OCR_ENGINE = 'windows-glm'
```

Server mode is useful when the GLM-OCR runtime takes time to initialize because the app can reuse the already-running service.

## Runtime probe

```powershell
npm run ocr:probe
```

The output includes `glmOcr.present`. `ocr:probe:strict` still checks the current MAA/Paddle prerequisites and does not require GLM-OCR.

## Notes

- GLM-OCR is slow compared with ROI Windows OCR and MAA ONNX. Keep it as a verification or fallback engine.
- The bridge sends only fixed ROI crops by default. It does not submit the full screenshot when regions are available.
- `RHODES_GLM_OCR_MAX_REGIONS` defaults to `12`; lower it when comparing only a few operator cards.
- Cloud MaaS mode is intentionally not the default because it sends screenshots outside the PC.

## Sources

- GLM-OCR README: https://github.com/zai-org/GLM-OCR
- GLM-OCR SDK install and usage: https://github.com/zai-org/GLM-OCR#glm-ocr-sdk
- GLM-OCR Flask service: https://github.com/zai-org/GLM-OCR#flask-service

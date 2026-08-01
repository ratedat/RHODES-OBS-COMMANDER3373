# GLM-OCR optional verification setup

GLM-OCR support is experimental and opt-in. RHODES OBS COMMANDER3373 does not bundle GLM-OCR, Python packages, or model files in the EXE. Use it only for local verification when the MAA-OCR profile route misses short operator names.

## OCR engine values

Set the engine from the app's `ADB / OCR接続` panel:

- `プロファイル既定`: existing profile routing. This is the default and keeps normal users on the current OCR path.
- `MAA-OCR`: uses the MAA-OCR route directly.
- `GLM-OCR 任意検証`: requires GLM-OCR and uses it directly. Use only when comparing raw GLM-OCR behavior.

The same values can be forced by environment variable:

```powershell
$env:RHODES_OCR_ENGINE = 'glm-ocr'
```

## App-managed install

For normal testers, use the app UI instead of installing Python manually:

1. Open `OBS設定`.
2. In `GLM-OCRランタイム`, press `状態確認`.
3. Press `ダウンロード/インストール`.
4. In `Ollamaローカル実行`, press `Ollama導入/モデル取得`.
5. Select `GLM-OCR 任意検証` as the OCR engine after both statuses become `使用可能`.

The app downloads `uv`, installs a managed Python 3.12 runtime, creates a dedicated venv, and installs `glmocr[selfhosted,server]`.
The Ollama installer downloads the official Windows ZIP release, extracts it into the app-managed runtime folder, writes a GLM-OCR Ollama config, starts `ollama serve`, and pulls `glm-ocr:latest`.

The runtime is stored under the app state directory:

- Portable EXE default: `RHODES OBS COMMANDER3373 Data/state/glm-ocr-runtime`
- Portable EXE Ollama default: `RHODES OBS COMMANDER3373 Data/state/ollama-runtime`
- Development default: `data/glm-ocr-runtime`
- Development Ollama default: `data/ollama-runtime`

`アンインストール` deletes that runtime directory, including the uv cache, Python runtime, venv, and model caches that RHODES directs into that folder.
For Ollama, `アンインストール` deletes the managed Ollama executable, GLM-OCR Ollama config, and the managed model directory.

After restarting the app, use `Ollamaローカル実行` -> `起動` before running GLM-OCR verification if the status says Ollama is not running.
The managed Ollama server uses `127.0.0.1:11435` instead of Ollama's usual `11434` so it does not accidentally reuse a user-installed Ollama server or model directory.

## Manual SDK mode

Manual setup is still available for developers who want to compare a custom environment. Do not add these packages to the app runtime environment for general users.

```powershell
py -3.12 -m venv .venv-glm-ocr
.\.venv-glm-ocr\Scripts\python.exe -m pip install --upgrade pip
.\.venv-glm-ocr\Scripts\python.exe -m pip install -r requirements-glm-ocr.txt
$env:RHODES_GLM_OCR_PYTHON = 'O:\Arknights_Rogue_OBSTool\.venv-glm-ocr\Scripts\python.exe'
$env:RHODES_OCR_ENGINE = 'glm-ocr'
```

The bridge imports `glmocr` and calls its Python `parse()` API for each fixed ROI crop.

## Local server mode

GLM-OCR also documents a Flask service. Start it separately, then point RHODES at the local endpoint:

```powershell
.\.venv-glm-ocr\Scripts\python.exe -m glmocr.server
$env:RHODES_GLM_OCR_MODE = 'server'
$env:RHODES_GLM_OCR_ENDPOINT = 'http://127.0.0.1:5002/glmocr/parse'
$env:RHODES_OCR_ENGINE = 'glm-ocr'
```

Server mode is useful when the GLM-OCR runtime takes time to initialize because the app can reuse the already-running service.

## Ollama local mode

The app-managed Ollama setup writes this GLM-OCR config automatically:

```yaml
pipeline:
  maas:
    enabled: false
  ocr_api:
    api_host: 127.0.0.1
    api_port: 11435
    api_path: /api/generate
    model: glm-ocr:latest
    api_mode: ollama_generate
```

For fixed ROI verification, the bridge passes each crop directly to Ollama through `RHODES_GLM_OCR_OLLAMA_ENDPOINT`.
This avoids the GLM-OCR SDK document-layout detector, which is unnecessary for small game UI crops.
The generated SDK config is still written for reference and future whole-document comparisons.

## Notes

- GLM-OCR is slow compared with the active MAAFramework/MAA-OCR route. Keep it as an optional verification engine, not the general default.
- The bridge sends only fixed ROI crops by default. It does not submit the full screenshot when regions are available.
- `RHODES_GLM_OCR_MAX_REGIONS` defaults to `12`; lower it when comparing only a few operator cards.
- Cloud MaaS mode is intentionally not the default because it sends screenshots outside the PC.

## Sources

- GLM-OCR README: https://github.com/zai-org/GLM-OCR
- GLM-OCR SDK install and usage: https://github.com/zai-org/GLM-OCR#glm-ocr-sdk
- GLM-OCR Flask service: https://github.com/zai-org/GLM-OCR#flask-service
- GLM-OCR Ollama deployment: https://github.com/zai-org/GLM-OCR/blob/main/examples/ollama-deploy/README.md
- Ollama releases: https://github.com/ollama/ollama/releases
- uv documentation: https://docs.astral.sh/uv/

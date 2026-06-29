import base64
import json
import os
import tempfile
import io
import urllib.error
import urllib.request
from pathlib import Path

try:
    from PIL import Image
except Exception as exc:
    raise RuntimeError(f"Pillow is required for GLM-OCR region crops: {exc}") from exc


def env_bool(name, default=False):
    value = os.environ.get(name)
    if value is None:
        return default
    return value.strip().lower() in {"1", "true", "yes", "on"}


def region_value(region, key, default):
    value = region.get(key, default) if isinstance(region, dict) else default
    if isinstance(value, list):
        return value[0] if value else default
    return value


def region_rect(region):
    if region is None:
        return None
    return {
        "x": float(region_value(region, "x", 0)),
        "y": float(region_value(region, "y", 0)),
        "width": float(region_value(region, "width", 1)),
        "height": float(region_value(region, "height", 1)),
    }


def clamp_region(region, image):
    x = max(0, int(round(float(region_value(region, "x", 0)))))
    y = max(0, int(round(float(region_value(region, "y", 0)))))
    width = max(1, int(round(float(region_value(region, "width", 1)))))
    height = max(1, int(round(float(region_value(region, "height", 1)))))
    right = min(image.width, x + width)
    bottom = min(image.height, y + height)
    if right <= x or bottom <= y:
        return (0, 0, image.width, image.height)
    return (x, y, right, bottom)


def crop_image(image_path, region, temp_dir):
    with Image.open(image_path) as image:
        crop = image.crop(clamp_region(region, image))
        scale = max(1, int(float(region_value(region, "scale", 1))))
        if scale > 1:
            crop = crop.resize((crop.width * scale, crop.height * scale), Image.Resampling.LANCZOS)
        path = Path(temp_dir) / f"{region_value(region, 'id', 'region')}-{len(os.listdir(temp_dir))}.png"
        crop.save(path)
        return str(path)


def split_text_lines(value):
    lines = []
    for line in str(value or "").replace("\r", "\n").split("\n"):
        cleaned = line.strip()
        if not cleaned:
            continue
        if cleaned in {"```", "---"}:
            continue
        cleaned = cleaned.strip("#*` ")
        if cleaned:
            lines.append(cleaned)
    return lines


def text_from_payload(payload):
    if not isinstance(payload, dict):
        return ""
    for key in ["text", "markdown", "md", "content"]:
        value = payload.get(key)
        if isinstance(value, str) and value.strip():
            return value
    result = payload.get("result")
    if isinstance(result, dict):
        for key in ["text", "markdown", "md", "content"]:
            value = result.get(key)
            if isinstance(value, str) and value.strip():
                return value
    raw = payload.get("raw_api_response")
    if isinstance(raw, str):
        return raw
    return ""


def coerce_python_result(result):
    if isinstance(result, dict):
        return result
    for attr in ["json_result", "json", "result"]:
        value = getattr(result, attr, None)
        if callable(value):
            value = value()
        if isinstance(value, dict):
            return value
    text = ""
    for attr in ["text", "markdown", "md"]:
        value = getattr(result, attr, None)
        if isinstance(value, str) and value.strip():
            text = value
            break
    return {"text": text or str(result or "")}


def parse_with_ollama(image_path, endpoint, timeout):
    prompt = os.environ.get(
        "RHODES_GLM_OCR_OLLAMA_PROMPT",
        "Text Recognition: Return only the visible Japanese operator name.",
    )
    model = os.environ.get("RHODES_GLM_OCR_OLLAMA_MODEL", "glm-ocr:latest")
    num_predict = int(os.environ.get("RHODES_GLM_OCR_OLLAMA_NUM_PREDICT", "24"))
    with Image.open(image_path) as image:
        buffer = io.BytesIO()
        image.convert("RGB").save(buffer, format="PNG")
    payload = {
        "model": model,
        "prompt": prompt,
        "images": [base64.b64encode(buffer.getvalue()).decode("ascii")],
        "stream": False,
        "options": {
            "temperature": 0,
            "num_predict": num_predict,
            "stop": ["\n", "---"],
        },
    }
    request = urllib.request.Request(
        endpoint,
        data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            result = json.loads(response.read().decode("utf-8"))
    except urllib.error.URLError as exc:
        raise RuntimeError(f"GLM-OCR Ollama request failed: {exc}") from exc
    text = str(result.get("response") or "").strip()
    return {"text": text, "raw_api_response": result}


def parse_with_python_sdk(image_path):
    config_path = os.environ.get("RHODES_GLM_OCR_CONFIG", "").strip()
    if config_path:
        try:
            from glmocr import GlmOcr
        except Exception as exc:
            raise RuntimeError(f"GLM-OCR is not available: {exc}") from exc
        with GlmOcr(config_path=config_path) as parser:
            return coerce_python_result(parser.parse(image_path))
    try:
        from glmocr import parse
    except Exception as exc:
        raise RuntimeError(f"GLM-OCR is not available: {exc}") from exc
    return coerce_python_result(parse(image_path))


def parse_with_server(image_path, endpoint, timeout):
    payload = json.dumps({"images": [image_path]}, ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        endpoint,
        data=payload,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.URLError as exc:
        raise RuntimeError(f"GLM-OCR server request failed: {exc}") from exc


def parse_image(image_path):
    mode = os.environ.get("RHODES_GLM_OCR_MODE", "auto").strip().lower()
    endpoint = os.environ.get("RHODES_GLM_OCR_ENDPOINT", "").strip()
    ollama_endpoint = os.environ.get("RHODES_GLM_OCR_OLLAMA_ENDPOINT", "").strip()
    timeout = float(os.environ.get("RHODES_GLM_OCR_REQUEST_TIMEOUT", "120"))
    if ollama_endpoint:
        return parse_with_ollama(image_path, ollama_endpoint, timeout)
    if mode in {"server", "http"} or endpoint:
        return parse_with_server(image_path, endpoint or "http://127.0.0.1:5002/glmocr/parse", timeout)
    return parse_with_python_sdk(image_path)


def main():
    image_path = os.environ["ARK_OCR_IMAGE"]
    regions = json.loads(os.environ.get("ARK_OCR_REGIONS_JSON") or "[]")
    include_full = env_bool("RHODES_GLM_OCR_INCLUDE_FULL", default=not bool(regions))
    max_regions = int(os.environ.get("RHODES_GLM_OCR_MAX_REGIONS", "12"))
    targets = regions[:max_regions] if regions else []
    all_results = []
    with tempfile.TemporaryDirectory(prefix="rhodes-glm-ocr-") as temp_dir:
        if include_full:
            payload = parse_image(image_path)
            for text in split_text_lines(text_from_payload(payload)):
                all_results.append({
                    "text": text,
                    "rawText": text,
                    "regionId": "full",
                    "roi": None,
                    "confidence": 0.55,
                    "source": "glm-ocr",
                })
        for region in targets:
            region_id = str(region_value(region, "id", "region"))
            crop_path = crop_image(image_path, region, temp_dir)
            payload = parse_image(crop_path)
            for text in split_text_lines(text_from_payload(payload)):
                all_results.append({
                    "text": text,
                    "rawText": text,
                    "regionId": region_id,
                    "roi": region_rect(region),
                    "confidence": 0.6,
                    "source": "glm-ocr",
                })
    output = {
        "text": " ".join(item["text"] for item in all_results if item.get("text")),
        "ocrResults": all_results,
        "engine": "glm-ocr",
    }
    encoded = base64.b64encode(json.dumps(output, ensure_ascii=False, separators=(",", ":")).encode("utf-8")).decode("ascii")
    print(encoded)


if __name__ == "__main__":
    main()

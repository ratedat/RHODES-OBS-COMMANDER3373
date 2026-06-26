import base64
import json
import math
import os
import tempfile
from pathlib import Path

try:
    import numpy as np
except Exception as exc:
    raise RuntimeError(f"NumPy is required for MAA ONNX OCR: {exc}") from exc

try:
    import onnxruntime as ort
except Exception as exc:
    raise RuntimeError(f"ONNXRuntime is required for MAA ONNX OCR: {exc}") from exc

try:
    from PIL import Image, ImageOps
except Exception as exc:
    raise RuntimeError(f"Pillow is required for MAA ONNX OCR: {exc}") from exc


def env_int(name, default):
    try:
        return int(os.environ.get(name, default))
    except Exception:
        return default


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


def crop_image(source_path, region, temp_dir):
    image = Image.open(source_path).convert("RGB")
    try:
        x = max(0, int(float(region_value(region, "x", 0))))
        y = max(0, int(float(region_value(region, "y", 0))))
        w = max(1, int(float(region_value(region, "width", 1))))
        h = max(1, int(float(region_value(region, "height", 1))))
        w = min(w, image.width - x)
        h = min(h, image.height - y)
        scale = max(1, int(float(region_value(region, "scale", 1))))
        crop = image.crop((x, y, x + w, y + h))
        if scale > 1:
            crop = crop.resize((crop.width * scale, crop.height * scale), Image.Resampling.LANCZOS)
        output = Path(temp_dir) / f"region-{region_value(region, 'id', 'region')}-{len(list(Path(temp_dir).glob('region-*.png')))}.png"
        crop.save(output)
        return str(output)
    finally:
        image.close()


def load_equivalence_classes(path):
    if not path or not Path(path).exists():
        return []
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    classes = []
    for group in data.get("equivalence_classes", []):
        if isinstance(group, list) and len(group) >= 2:
            normalized = [str(item) for item in group if str(item)]
            if len(normalized) >= 2:
                classes.append(normalized)
    return classes


def apply_equivalence_classes(value, classes):
    text = str(value or "")
    for group in classes:
        replacement = group[0]
        for variant in group[1:]:
            text = text.replace(variant, replacement)
    return text


def preprocess_image(image_path):
    image = Image.open(image_path).convert("RGB")
    try:
        mode = os.environ.get("RHODES_MAA_ONNX_PREPROCESS", "rgb").strip().lower()
        if mode == "gray":
            image = ImageOps.grayscale(image).convert("RGB")
        elif mode == "invert":
            image = ImageOps.invert(ImageOps.grayscale(image)).convert("RGB")

        target_h = env_int("RHODES_MAA_ONNX_REC_HEIGHT", 48)
        width_multiple = max(1, env_int("RHODES_MAA_ONNX_WIDTH_MULTIPLE", 32))
        min_width = max(width_multiple, env_int("RHODES_MAA_ONNX_MIN_WIDTH", 32))
        max_width = max(min_width, env_int("RHODES_MAA_ONNX_MAX_WIDTH", 4096))
        ratio = image.width / max(1, image.height)
        target_w = int(math.ceil((target_h * ratio) / width_multiple) * width_multiple)
        target_w = max(min_width, min(target_w, max_width))
        image = image.resize((target_w, target_h), Image.Resampling.BICUBIC)
        arr = np.asarray(image).astype("float32") / 255.0
        arr = (arr - 0.5) / 0.5
        arr = np.transpose(arr, (2, 0, 1))[None, :, :, :]
        return arr
    finally:
        image.close()


def load_keys(path):
    keys_path = Path(path)
    if not keys_path.exists():
        raise RuntimeError(f"MAA ONNX OCR keys not found: {keys_path}")
    keys = keys_path.read_text(encoding="utf-8").splitlines()
    return [""] + keys


def ctc_decode(probabilities, characters):
    if probabilities.ndim == 3:
        probabilities = probabilities[0]
    indices = np.argmax(probabilities, axis=1)
    scores = np.max(probabilities, axis=1)
    chars = []
    char_scores = []
    previous = -1
    for raw_index, raw_score in zip(indices, scores):
        index = int(raw_index)
        if index != 0 and index != previous:
            char = characters[index] if index < len(characters) else ""
            if char:
                chars.append(char)
                char_scores.append(float(raw_score))
        previous = index
    confidence = sum(char_scores) / len(char_scores) if char_scores else 0.0
    return "".join(chars), confidence


def main():
    image_path = os.environ["ARK_OCR_IMAGE"]
    regions = json.loads(os.environ.get("ARK_OCR_REGIONS_JSON") or "[]")
    model_path = Path(os.environ["RHODES_MAA_ONNX_REC_MODEL"])
    if not model_path.exists():
        raise RuntimeError(f"MAA ONNX OCR model not found: {model_path}")
    keys = load_keys(os.environ["RHODES_MAA_ONNX_REC_KEYS"])
    equivalence_classes = load_equivalence_classes(os.environ.get("RHODES_MAA_OCR_CONFIG"))

    session = ort.InferenceSession(str(model_path), providers=["CPUExecutionProvider"])
    input_name = session.get_inputs()[0].name
    output_name = session.get_outputs()[0].name
    all_results = []

    with tempfile.TemporaryDirectory(prefix="rhodes-maa-onnx-ocr-") as temp_dir:
        targets = regions or [{"id": "full", "x": 0, "y": 0, "width": Image.open(image_path).width, "height": Image.open(image_path).height, "scale": 1}]
        for region in targets:
            region_id = str(region_value(region, "id", "region"))
            crop_path = crop_image(image_path, region, temp_dir)
            tensor = preprocess_image(crop_path)
            output = session.run([output_name], {input_name: tensor})[0]
            raw_text, confidence = ctc_decode(output, keys)
            text = apply_equivalence_classes(raw_text, equivalence_classes)
            if not text.strip():
                continue
            all_results.append({
                "text": text,
                "rawText": raw_text,
                "regionId": region_id,
                "roi": region_rect(region),
                "confidence": confidence,
            })

    payload = {
        "text": " ".join(item["text"] for item in all_results if item.get("text")),
        "ocrResults": all_results,
        "engine": "maa-onnx-recognition",
    }
    encoded = base64.b64encode(json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")).decode("ascii")
    print(encoded)


if __name__ == "__main__":
    main()

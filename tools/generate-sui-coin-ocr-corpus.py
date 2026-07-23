#!/usr/bin/env python3
"""Generate synthetic IS#6 coin-name OCR fixtures with Noto Sans JP."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from datetime import datetime, timezone
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont


BASE_WIDTH = 1280
BASE_HEIGHT = 720
LINE_WIDTH = 220
LINE_HEIGHT = 32
SHEET_COLUMNS = 3
SHEET_ROWS = 6
SHEET_ORIGIN_X = 170
SHEET_ORIGIN_Y = 116
SHEET_STEP_X = 340
SHEET_STEP_Y = 76

VARIANTS = {
    "active": {"background": (20, 24, 23), "text": (242, 242, 236), "blur": 0.0},
    "inactive": {"background": (18, 22, 21), "text": (178, 184, 179), "blur": 0.0},
    "dim": {"background": (15, 19, 18), "text": (124, 132, 127), "blur": 0.0},
    "soft": {"background": (18, 22, 21), "text": (181, 187, 181), "blur": 0.45},
}


def parse_csv(value: str) -> list[str]:
    return [item.strip() for item in value.split(",") if item.strip()]


def resolve_default_font() -> Path:
    candidates = [
        Path(r"C:\Windows\Fonts\NotoSansJP-VF.ttf"),
        Path(r"C:\Windows\Fonts\NotoSansJP-Regular.otf"),
    ]
    return next((path for path in candidates if path.exists()), candidates[0])


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_coins(data_path: Path, limit: int | None) -> list[dict]:
    payload = json.loads(data_path.read_text(encoding="utf-8"))
    source = payload.get("selectableEffects", payload) if isinstance(payload, dict) else payload
    coins = [
        item
        for item in source
        if item.get("campaignId") == "is6_sui" and item.get("slot") == "coin"
    ]
    coins.sort(key=lambda item: (int(item.get("order", 0)), item.get("id", "")))
    return coins[:limit] if limit is not None else coins


def display_label(coin: dict) -> str:
    category = str(coin.get("category", "")).strip()
    prefix = category[:1] if category else "銭"
    return f"{prefix}-{coin['name']}"


def load_font(font_path: Path, size: int, scale: int) -> ImageFont.FreeTypeFont:
    font = ImageFont.truetype(str(font_path), size * scale)
    try:
        font.set_variation_by_name("Regular")
    except (AttributeError, OSError, ValueError):
        pass
    return font


def render_line(label: str, font_path: Path, font_size: int, variant_name: str) -> Image.Image:
    config = VARIANTS[variant_name]
    scale = 4
    canvas = Image.new(
        "RGB",
        (LINE_WIDTH * scale, LINE_HEIGHT * scale),
        config["background"],
    )
    draw = ImageDraw.Draw(canvas)
    font = load_font(font_path, font_size, scale)
    bbox = draw.textbbox((0, 0), label, font=font, stroke_width=0)
    text_height = bbox[3] - bbox[1]
    y = ((LINE_HEIGHT * scale - text_height) // 2) - bbox[1]
    draw.text((6 * scale, y), label, font=font, fill=config["text"])
    line = canvas.resize((LINE_WIDTH, LINE_HEIGHT), Image.Resampling.LANCZOS)
    if config["blur"] > 0:
        line = line.filter(ImageFilter.GaussianBlur(config["blur"]))
    return line


def build_sheet(samples: list[dict], output_path: Path) -> None:
    sheet = Image.new("RGB", (BASE_WIDTH, BASE_HEIGHT), (9, 13, 12))
    for index, sample in enumerate(samples):
        row = index // SHEET_COLUMNS
        column = index % SHEET_COLUMNS
        x = SHEET_ORIGIN_X + (column * SHEET_STEP_X)
        y = SHEET_ORIGIN_Y + (row * SHEET_STEP_Y)
        line = Image.open(sample["absoluteLinePath"])
        sheet.paste(line, (x, y))
        sample["sheetRoi"] = [x, y, LINE_WIDTH, LINE_HEIGHT]
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output_path, optimize=True)


def main() -> int:
    repo_root = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--data", type=Path, default=repo_root / "data" / "selectable-effects.json")
    parser.add_argument("--font", type=Path, default=resolve_default_font())
    parser.add_argument(
        "--output",
        type=Path,
        default=repo_root / ".agent-work" / "ocr-corpus" / "is6-sui-coins",
    )
    parser.add_argument("--sizes", default="14,15,16,17,18,20")
    parser.add_argument("--variants", default="active,inactive,dim,soft")
    parser.add_argument("--limit", type=int)
    args = parser.parse_args()

    sizes = [int(value) for value in parse_csv(args.sizes)]
    variant_names = parse_csv(args.variants)
    unknown_variants = [name for name in variant_names if name not in VARIANTS]
    if unknown_variants:
        parser.error(f"unknown variants: {', '.join(unknown_variants)}")
    if not args.font.exists():
        parser.error(f"font not found: {args.font}")

    output_root = args.output.resolve()
    output_root.mkdir(parents=True, exist_ok=True)
    coins = load_coins(args.data.resolve(), args.limit)
    samples: list[dict] = []
    sheets: list[dict] = []

    for variant_name in variant_names:
        for font_size in sizes:
            variant_samples: list[dict] = []
            for coin in coins:
                relative_line_path = Path("lines") / variant_name / f"size-{font_size}" / f"{coin['id']}.png"
                absolute_line_path = output_root / relative_line_path
                absolute_line_path.parent.mkdir(parents=True, exist_ok=True)
                label = display_label(coin)
                render_line(label, args.font, font_size, variant_name).save(absolute_line_path, optimize=True)
                sample = {
                    "coinId": coin["id"],
                    "name": coin["name"],
                    "category": coin.get("category", ""),
                    "expectedText": coin["name"],
                    "displayText": label,
                    "fontSize": font_size,
                    "variant": variant_name,
                    "linePath": relative_line_path.as_posix(),
                    "lineRoi": [0, 0, LINE_WIDTH, LINE_HEIGHT],
                    "absoluteLinePath": str(absolute_line_path),
                }
                samples.append(sample)
                variant_samples.append(sample)

            per_sheet = SHEET_COLUMNS * SHEET_ROWS
            sheet_count = math.ceil(len(variant_samples) / per_sheet)
            for sheet_index in range(sheet_count):
                start = sheet_index * per_sheet
                batch = variant_samples[start : start + per_sheet]
                relative_sheet_path = (
                    Path("sheets")
                    / variant_name
                    / f"size-{font_size}"
                    / f"sheet-{sheet_index + 1:02d}.png"
                )
                build_sheet(batch, output_root / relative_sheet_path)
                for sample in batch:
                    sample["sheetPath"] = relative_sheet_path.as_posix()
                sheets.append(
                    {
                        "variant": variant_name,
                        "fontSize": font_size,
                        "path": relative_sheet_path.as_posix(),
                        "sampleCount": len(batch),
                    }
                )

    for sample in samples:
        sample.pop("absoluteLinePath", None)

    manifest = {
        "schemaVersion": 1,
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "campaignId": "is6_sui",
        "purpose": "MAA-OCR regression and coin-name correction evaluation",
        "sourceData": str(args.data.resolve()),
        "font": {
            "path": str(args.font.resolve()),
            "sha256": sha256(args.font.resolve()),
            "familyAssumption": "Noto Sans JP Regular",
        },
        "baseResolution": [BASE_WIDTH, BASE_HEIGHT],
        "lineSize": [LINE_WIDTH, LINE_HEIGHT],
        "fontSizes": sizes,
        "variants": variant_names,
        "coinCount": len(coins),
        "sampleCount": len(samples),
        "samples": samples,
        "sheets": sheets,
    }
    (output_root / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    with (output_root / "targets.jsonl").open("w", encoding="utf-8", newline="\n") as handle:
        for sample in samples:
            handle.write(json.dumps(sample, ensure_ascii=False, separators=(",", ":")) + "\n")

    print(
        json.dumps(
            {
                "output": str(output_root),
                "coins": len(coins),
                "samples": len(samples),
                "sheets": len(sheets),
            },
            ensure_ascii=False,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

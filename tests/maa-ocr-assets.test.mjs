import test from "node:test";
import assert from "node:assert/strict";

import { buildMaaOcrAssetManifest, maaOcrAssets, maaOcrRawUrl } from "../app/domain/recognition/maa-ocr-assets.js";

test("MAA OCR asset list excludes ONNX model binaries by default", () => {
  const assets = maaOcrAssets();

  assert.equal(assets.some((asset) => asset.localPath.endsWith("inference.onnx")), false);
  assert.equal(assets.some((asset) => asset.id === "jp.ocr_config"), true);
  assert.equal(assets.some((asset) => asset.id === "jp.rec.keys"), true);
  assert.equal(assets.some((asset) => asset.id === "jp.tasks"), true);
  assert.equal(assets.some((asset) => asset.id === "roguelike.sarkaz.tasks"), true);
  assert.equal(assets.some((asset) => asset.id === "roguelike.recruit.elite-0"), true);
  assert.equal(assets.some((asset) => asset.id === "roguelike.recruit.ocr-flag"), true);
});

test("MAA OCR asset list can include optional ONNX model binaries", () => {
  const assets = maaOcrAssets({ includeModels: true });

  assert.equal(assets.some((asset) => asset.id === "common.det.onnx"), true);
  assert.equal(assets.some((asset) => asset.id === "jp.rec.onnx"), true);
});

test("MAA OCR raw URL points at the dev-v2 source asset", () => {
  const url = maaOcrRawUrl({ rawPath: "resource/global/YoStarJP/resource/ocr_config.json" });

  assert.equal(url, "https://raw.githubusercontent.com/MaaAssistantArknights/MaaAssistantArknights/dev-v2/resource/global/YoStarJP/resource/ocr_config.json");
});

test("MAA OCR manifest records local asset presence without reading files itself", () => {
  const manifest = buildMaaOcrAssetManifest({ exists: (localPath) => localPath.endsWith("keys.txt") });
  const keys = manifest.assets.find((asset) => asset.id === "jp.rec.keys");
  const config = manifest.assets.find((asset) => asset.id === "jp.ocr_config");

  assert.equal(keys.present, true);
  assert.equal(config.present, false);
});

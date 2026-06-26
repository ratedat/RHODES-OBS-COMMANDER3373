import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

import { MAA_NUMBER_OCR_REPLACE, applyOcrReplace, normalizeRecognitionText } from "../app/domain/recognition/text-normalize.js";

test("MAA number OCR normalization applies NumberOcrReplace-compatible rules", () => {
  assert.equal(normalizeRecognitionText("IO B 台 { 十", ["maa_number"]), "10881+");
});

test("applyOcrReplace applies regex replacements in order", () => {
  assert.equal(applyOcrReplace("ブループリント分 OCR崩れ", [["ブループリント分.*", "ブループリント分隊"]]), "ブループリント分隊");
});

test("MAA number OCR normalization stays in sync with vendored NumberOcrReplace", async () => {
  const rules = JSON.parse(await readFile(new URL("../data/recognition/maa-ocr-rules.json", import.meta.url), "utf8"));

  assert.deepEqual(MAA_NUMBER_OCR_REPLACE, rules.numberOcrReplace);
});

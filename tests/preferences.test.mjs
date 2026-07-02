import test from "node:test";
import assert from "node:assert/strict";

import { normalizeOcrEngine, normalizePreferences, ocrEngineOptions } from "../app/lib/preferences.js";

test("OCR engine preference defaults to MAA-OCR", () => {
  assert.equal(normalizeOcrEngine(""), "maa-ocr");
  assert.equal(normalizeOcrEngine("unknown"), "maa-ocr");
  assert.equal(normalizeOcrEngine("profile"), "maa-ocr");
  assert.equal(normalizePreferences({}).ocrEngine, "maa-ocr");
});

test("OCR engine preference accepts GLM verification engines", () => {
  assert.equal(normalizeOcrEngine("glm-ocr"), "glm-ocr");
  assert.equal(normalizeOcrEngine("windows-glm"), "glm-ocr");
  assert.ok(ocrEngineOptions.some((option) => option.id === "glm-ocr"));
});

test("OCR engine preference exposes only MAA-OCR plus optional GLM", () => {
  assert.equal(normalizeOcrEngine("maa-ocr"), "maa-ocr");
  assert.equal(normalizeOcrEngine("maa-onnx"), "maa-ocr");
  assert.equal(normalizeOcrEngine("hybrid"), "maa-ocr");
  assert.equal(normalizeOcrEngine("paddle"), "maa-ocr");
  assert.deepEqual(ocrEngineOptions.map((option) => option.id), ["maa-ocr", "glm-ocr"]);
});

test("choice list filter preferences are normalized", () => {
  const preferences = normalizePreferences({
    operatorShowSelectedFirst: "true",
    operatorHideExcluded: false,
    operatorSelectedOnly: "1",
    operatorExcludedIds: ["texas", "", "texas", "exusiai"],
    relicShowSelectedFirst: true,
    relicHideExcluded: "false",
    relicSelectedOnly: 0,
    relicExcludedIds: ["is5_sarkaz_relic_001", null, "is5_sarkaz_relic_001"],
  });

  assert.equal(preferences.operatorShowSelectedFirst, true);
  assert.equal(preferences.operatorHideExcluded, false);
  assert.equal(preferences.operatorSelectedOnly, true);
  assert.deepEqual(preferences.operatorExcludedIds, ["texas", "exusiai"]);
  assert.equal(preferences.relicShowSelectedFirst, true);
  assert.equal(preferences.relicHideExcluded, false);
  assert.equal(preferences.relicSelectedOnly, false);
  assert.deepEqual(preferences.relicExcludedIds, ["is5_sarkaz_relic_001"]);
});

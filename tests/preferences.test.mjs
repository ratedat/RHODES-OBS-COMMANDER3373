import test from "node:test";
import assert from "node:assert/strict";

import { normalizeOcrEngine, normalizePreferences, ocrEngineOptions } from "../app/lib/preferences.js";
import {
  resolveOverlayBackgroundAlpha,
  shouldShowOverlayPartTitles,
} from "../app/lib/overlay-config.js";

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

test("overlay background remains opaque when transparent background is disabled", () => {
  const preferences = normalizePreferences({
    sukiOutputTransparentBackground: false,
    sukiOutputBackgroundTransparency: 100,
  });

  assert.equal(resolveOverlayBackgroundAlpha(preferences), 1);
});

test("overlay background transparency is clamped and applied only when enabled", () => {
  assert.equal(resolveOverlayBackgroundAlpha(normalizePreferences({
    sukiOutputTransparentBackground: true,
    sukiOutputBackgroundTransparency: 35,
  })), 0.65);
  assert.equal(resolveOverlayBackgroundAlpha(normalizePreferences({
    sukiOutputTransparentBackground: true,
    sukiOutputBackgroundTransparency: 140,
  })), 0);
});

test("individual overlay titles default to visible and can be hidden", () => {
  assert.equal(shouldShowOverlayPartTitles(normalizePreferences({})), true);
  assert.equal(shouldShowOverlayPartTitles(normalizePreferences({
    sukiOutputShowPartTitles: false,
  })), false);
});

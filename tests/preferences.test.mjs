import test from "node:test";
import assert from "node:assert/strict";

import { normalizeOcrEngine, normalizePreferences, ocrEngineOptions } from "../app/lib/preferences.js";

test("OCR engine preference defaults to profile routing", () => {
  assert.equal(normalizeOcrEngine(""), "profile");
  assert.equal(normalizeOcrEngine("unknown"), "profile");
  assert.equal(normalizePreferences({}).ocrEngine, "profile");
});

test("OCR engine preference accepts GLM verification engines", () => {
  assert.equal(normalizeOcrEngine("glm-ocr"), "glm-ocr");
  assert.equal(normalizeOcrEngine("windows-glm"), "windows-glm");
  assert.ok(ocrEngineOptions.some((option) => option.id === "windows-glm"));
});

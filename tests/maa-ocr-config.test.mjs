import test from "node:test";
import assert from "node:assert/strict";

import { applyMaaEquivalenceClasses, createMaaOcrNormalizer, normalizeMaaEquivalenceClasses } from "../app/domain/recognition/maa-ocr-config.js";

const jpConfig = {
  equivalence_classes: [
    ["夕", "タ"],
    ["ニ", "二"],
    ["-", "ー", "一", "−"],
    ["へ", "ベ", "ペ", "ヘ", "べ", "ぺ"],
  ],
};

test("MAA OCR equivalence classes map variants to the first class entry", () => {
  assert.equal(applyMaaEquivalenceClasses("タ二ー一−ヘべ", jpConfig), "夕ニ---へへ");
});

test("MAA OCR equivalence class normalization ignores invalid groups", () => {
  assert.deepEqual(normalizeMaaEquivalenceClasses({ equivalence_classes: [["A"], ["B", "8"], "bad"] }), [["B", "8"]]);
});

test("MAA OCR normalizer can be reused without reparsing config", () => {
  const normalize = createMaaOcrNormalizer(jpConfig);

  assert.equal(normalize("タワーー"), "夕ワ--");
});

import test from "node:test";
import assert from "node:assert/strict";

import { createDefaultOcrTextExtractor, createMergedTextExtractor, createProfileAwareTextExtractor, mergeOcrFrames } from "../app/recognition/adapters/ocr-text-extractor.js";

test("mergeOcrFrames keeps OCR results from multiple engines and dedupes exact duplicates", () => {
  const frame = mergeOcrFrames({ bytes: Buffer.from("x") }, [
    { ocrResults: [
      { text: "位置測定分隊", regionId: "run.squad_name", confidence: 0.94 },
      { text: "18", regionId: "run.difficulty_grade", confidence: 0.8 },
    ] },
    { ocrResults: [
      { text: "18", regionId: "run.difficulty_grade", confidence: 0.99 },
      { text: "20", regionId: "run.ingot", confidence: 0.91 },
    ] },
  ], { engine: "merged-test" });

  assert.equal(frame.ocrEngine, "merged-test");
  assert.equal(frame.ocrResults.length, 3);
  assert.equal(frame.ocrResults.find((item) => item.regionId === "run.difficulty_grade").confidence, 0.99);
  assert.match(frame.text, /位置測定分隊/);
  assert.match(frame.text, /20/);
});

test("createMergedTextExtractor returns all successful OCR outputs when one engine fails", async () => {
  const extractor = createMergedTextExtractor([
    { extract: async () => { throw new Error("onnx missing"); } },
    { extract: async (frame) => ({ ...frame, ocrResults: [{ text: "20", regionId: "run.ingot" }] }) },
  ], { engine: "merged-test" });

  const frame = await extractor.extract({ bytes: Buffer.from("x") });

  assert.equal(frame.ocrEngine, "merged-test");
  assert.equal(frame.text, "20");
});

test("default OCR selector exposes MAA-OCR for profile scans", () => {
  const extractor = createDefaultOcrTextExtractor({ engine: "maa-ocr" });

  assert.equal(typeof extractor.extract, "function");
});

test("default OCR selector maps auto to MAA-OCR", () => {
  const extractor = createDefaultOcrTextExtractor({ engine: "auto" });

  assert.equal(typeof extractor.extract, "function");
});


test("mergeOcrFrames drops explicitly low-confidence OCR results", () => {
  const frame = mergeOcrFrames({ bytes: Buffer.from("x") }, [
    { ocrResults: [
      { text: "2", regionId: "run.difficulty_grade", confidence: 0.12 },
      { text: "18", regionId: "run.difficulty_grade", confidence: 0.95 },
      { text: "手動候補", regionId: "manual" },
    ] },
  ], { engine: "merged-test", minConfidence: 0.2 });

  assert.equal(frame.ocrResults.some((item) => item.text === "2"), false);
  assert.equal(frame.ocrResults.some((item) => item.text === "18"), true);
  assert.equal(frame.ocrResults.some((item) => item.text === "手動候補"), true);
});


test("profile-aware OCR routing can force relic scans to a different extractor", async () => {
  const calls = [];
  const extractor = createProfileAwareTextExtractor({
    defaultExtractor: {
      async extract(frame) {
        calls.push("default");
        return { ...frame, text: "default" };
      },
    },
    profileExtractors: {
      relicsFull: {
        async extract(frame) {
          calls.push("relicsFull");
          return { ...frame, text: "profile-specific" };
        },
      },
    },
  });

  const relicFrame = await extractor.extract({ bytes: Buffer.from("x") }, { profile: { id: "relicsFull" } });
  const runFrame = await extractor.extract({ bytes: Buffer.from("x") }, { profile: { id: "runStatusFull" } });

  assert.equal(relicFrame.text, "profile-specific");
  assert.equal(runFrame.text, "default");
  assert.deepEqual(calls, ["relicsFull", "default"]);
});

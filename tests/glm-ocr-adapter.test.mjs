import test from "node:test";
import assert from "node:assert/strict";

import {
  createGlmOcrTextExtractor,
  normalizeGlmOcrPayload,
  parseGlmOcrStdout,
} from "../app/recognition/adapters/glm-ocr-adapter.js";

test("GLM OCR stdout parser decodes UTF-8 Japanese JSON from the final base64 line", () => {
  const payload = {
    engine: "glm-ocr",
    text: "グム W",
    ocrResults: [{ text: "グム", rawText: "グム", regionId: "operator.card.name.1" }],
  };
  const encoded = Buffer.from(JSON.stringify(payload), "utf8").toString("base64");

  assert.deepEqual(parseGlmOcrStdout(`glm log\n${encoded}\n`), payload);
});

test("GLM OCR payload normalization preserves raw text and region metadata", () => {
  const payload = normalizeGlmOcrPayload({
    engine: "glm-ocr",
    ocrResults: [
      { text: "W", rawText: "監W_", regionId: "operator.card.name.2", roi: { x: 1, y: 2, width: 3, height: 4 }, confidence: 0.62 },
      { text: "" },
    ],
  });

  assert.deepEqual(payload, {
    engine: "glm-ocr",
    text: "W",
    ocrResults: [{ text: "W", rawText: "監W_", regionId: "operator.card.name.2", roi: { x: 1, y: 2, width: 3, height: 4 }, confidence: 0.62, source: "glm-ocr" }],
  });
});

test("GLM OCR extractor can use an injected runner and returns OCR text", async () => {
  const encoded = Buffer.from(JSON.stringify({
    engine: "glm-ocr",
    text: "グム",
    ocrResults: [{ text: "グム", rawText: "グム", regionId: "operator.card.name.1", confidence: 0.6 }],
  }), "utf8").toString("base64");
  const extractor = createGlmOcrTextExtractor({
    pythonPath: "X:/glm/python.exe",
    extraEnv: { HF_HOME: "X:/glm/cache/hf" },
    runOcr: async ({ regions, pythonPath, extraEnv }) => {
      assert.equal(regions[0].id, "operator.card.name.1");
      assert.equal(pythonPath, "X:/glm/python.exe");
      assert.equal(extraEnv.HF_HOME, "X:/glm/cache/hf");
      return encoded;
    },
  });

  const frame = await extractor.extract({ bytes: Buffer.from("image") }, { regions: [{ id: "operator.card.name.1" }] });

  assert.equal(frame.ocrEngine, "glm-ocr");
  assert.equal(frame.text, "グム");
  assert.equal(frame.ocrResults[0].source, "glm-ocr");
});

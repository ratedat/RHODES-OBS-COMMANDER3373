import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

import { normalizePaddleOcrPayload, parsePaddleOcrStdout, resolvePaddlePythonExecutable } from "../app/recognition/adapters/paddle-ocr-adapter.js";
import { createFallbackTextExtractor } from "../app/recognition/adapters/ocr-text-extractor.js";

test("Paddle OCR stdout parser decodes UTF-8 Japanese JSON from the final base64 line", () => {
  const payload = {
    engine: "paddleocr",
    text: "耐久値 4 / 4 指揮Lv 1",
    ocrResults: [{ text: "耐久値", regionId: "run.status_band" }],
  };
  const encoded = Buffer.from(JSON.stringify(payload), "utf8").toString("base64");

  assert.deepEqual(parsePaddleOcrStdout(`download log\n${encoded}\n`), payload);
});

test("Paddle OCR payload normalization keeps text, region IDs, and confidence", () => {
  const payload = normalizePaddleOcrPayload({
    engine: "paddleocr",
    text: "4 / 4",
    ocrResults: [
      { text: "4 / 4", regionId: "run.life_points", roi: { x: 370, y: 74, width: 76, height: 38 }, confidence: 0.91 },
      { text: "" },
    ],
  });

  assert.deepEqual(payload, {
    engine: "paddleocr",
    text: "4 / 4",
    ocrResults: [{ text: "4 / 4", regionId: "run.life_points", roi: { x: 370, y: 74, width: 76, height: 38 }, confidence: 0.91 }],
  });
});

test("fallback text extractor uses the next OCR engine when the first one fails", async () => {
  const extractor = createFallbackTextExtractor([
    { extract: async () => { throw new Error("missing paddleocr"); } },
    { extract: async (frame) => ({ ...frame, text: "Windows OCR fallback", ocrResults: [{ text: "fallback" }] }) },
  ]);

  const frame = await extractor.extract({ bytes: Buffer.from("x") });

  assert.equal(frame.text, "Windows OCR fallback");
});

test("Paddle OCR python resolver prefers shared PaddleOCR venv before generic python when project venv is absent", async () => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ocr-empty-project-"));
  try {
    const resolved = resolvePaddlePythonExecutable({}, "C:\Users\owner", dir);
    assert.equal(resolved.endsWith(".paddleocr-mcp-venv\Scripts\python.exe") || resolved === "python", true);
  } finally {
    await fs.rm(dir, { recursive: true, force: true });
  }
});

test("Paddle OCR python resolver keeps explicit RHODES_PYTHON", () => {
  assert.equal(resolvePaddlePythonExecutable({ RHODES_PYTHON: "X:\\custom\\python.exe" }, "C:\\Users\\owner"), "X:\\custom\\python.exe");
});

test("Paddle OCR python resolver prefers project .venv-ocr before shared OCR venv", async () => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ocr-resolver-"));
  const pythonPath = path.join(dir, ".venv-ocr", "Scripts", "python.exe");
  await fs.mkdir(path.dirname(pythonPath), { recursive: true });
  await fs.writeFile(pythonPath, "");

  try {
    assert.equal(resolvePaddlePythonExecutable({}, "C:\\Users\\owner", dir), pythonPath);
  } finally {
    await fs.rm(dir, { recursive: true, force: true });
  }
});

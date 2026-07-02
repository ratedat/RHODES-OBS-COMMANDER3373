import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

test("OCR runtime probe uses the shared MAA/GLM Python resolver, not legacy Paddle", async () => {
  const source = await readFile(new URL("../tools/probe-ocr-runtime.mjs", import.meta.url), "utf8");

  assert.match(source, /resolveOcrPythonExecutable/);
  assert.doesNotMatch(source, /paddle-ocr-adapter/);
  assert.doesNotMatch(source, /resolvePaddlePythonExecutable/);
  assert.doesNotMatch(source, /paddleocr/);
  assert.doesNotMatch(source, /fastdeploy/);
  assert.doesNotMatch(source, /windows-glm/);
});

test("OCR requirements install only the active MAA-OCR base dependencies", async () => {
  const source = await readFile(new URL("../requirements-ocr.txt", import.meta.url), "utf8");

  assert.match(source, /onnxruntime/);
  assert.match(source, /numpy/);
  assert.match(source, /Pillow/);
  assert.doesNotMatch(source, /paddlepaddle/i);
  assert.doesNotMatch(source, /^paddleocr/im);
  assert.doesNotMatch(source, /fastdeploy/i);
});

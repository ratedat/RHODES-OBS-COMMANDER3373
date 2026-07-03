import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

import { resolveOcrPythonExecutable } from "../app/recognition/adapters/ocr-python-resolver.js";

test("OCR python resolver prefers explicit GLM OCR python path", () => {
  assert.equal(
    resolveOcrPythonExecutable({
      RHODES_GLM_OCR_PYTHON: "X:\\glm\\python.exe",
      RHODES_PYTHON: "X:\\legacy\\python.exe",
    }, "C:\\Users\\owner"),
    "X:\\glm\\python.exe",
  );
});

test("OCR python resolver keeps explicit RHODES_PYTHON as a compatibility fallback", () => {
  assert.equal(resolveOcrPythonExecutable({ RHODES_PYTHON: "X:\\custom\\python.exe" }, "C:\\Users\\owner"), "X:\\custom\\python.exe");
});

test("OCR python resolver ignores retired project MAA OCR venv", async () => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ocr-resolver-"));
  const homeDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ocr-home-"));
  const pythonPath = path.join(dir, ".venv-ocr", "Scripts", "python.exe");
  await fs.mkdir(path.dirname(pythonPath), { recursive: true });
  await fs.writeFile(pythonPath, "");

  try {
    assert.equal(resolveOcrPythonExecutable({}, homeDir, dir), "python");
  } finally {
    await fs.rm(dir, { recursive: true, force: true });
    await fs.rm(homeDir, { recursive: true, force: true });
  }
});

test("OCR python resolver can use project GLM OCR venv", async () => {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-glm-resolver-"));
  const pythonPath = path.join(dir, ".venv-glm-ocr", "Scripts", "python.exe");
  await fs.mkdir(path.dirname(pythonPath), { recursive: true });
  await fs.writeFile(pythonPath, "");

  try {
    assert.equal(resolveOcrPythonExecutable({}, "C:\\Users\\owner", dir), pythonPath);
  } finally {
    await fs.rm(dir, { recursive: true, force: true });
  }
});

test("OCR python resolver ignores legacy PaddleOCR shared venv", async () => {
  const homeDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ocr-home-"));
  const cwd = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ocr-project-"));
  const legacyPythonPath = path.join(homeDir, ".paddleocr-mcp-venv", "Scripts", "python.exe");
  await fs.mkdir(path.dirname(legacyPythonPath), { recursive: true });
  await fs.writeFile(legacyPythonPath, "");

  try {
    assert.equal(resolveOcrPythonExecutable({}, homeDir, cwd), "python");
  } finally {
    await fs.rm(homeDir, { recursive: true, force: true });
    await fs.rm(cwd, { recursive: true, force: true });
  }
});

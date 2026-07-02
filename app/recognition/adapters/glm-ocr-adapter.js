import { execFile } from "node:child_process";
import { randomUUID } from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

import { resolveOcrPythonExecutable } from "./ocr-python-resolver.js";

const BRIDGE_SCRIPT = fileURLToPath(new URL("./glm-ocr-bridge.py", import.meta.url));

function pythonExecutable() {
  return resolveOcrPythonExecutable();
}

async function bridgeSource() {
  return fs.readFile(BRIDGE_SCRIPT, "utf8");
}

export function runPythonGlmOcr({
  imagePath,
  regions = [],
  timeoutMs = 180000,
  pythonPath = pythonExecutable(),
  extraEnv = {},
} = {}) {
  return new Promise(async (resolve, reject) => {
    let script;
    try {
      script = await bridgeSource();
    } catch (error) {
      reject(error);
      return;
    }
    execFile(pythonPath, ["-c", script], {
      encoding: "utf8",
      windowsHide: true,
      timeout: timeoutMs,
      maxBuffer: 32 * 1024 * 1024,
      env: {
        ...process.env,
        ...extraEnv,
        ARK_OCR_IMAGE: imagePath,
        ARK_OCR_REGIONS_JSON: JSON.stringify(regions),
      },
    }, (error, stdout, stderr) => {
      if (error) {
        error.stderr = stderr;
        reject(error);
        return;
      }
      resolve(stdout);
    });
  });
}

export function parseGlmOcrStdout(stdout) {
  const lines = String(stdout || "").split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
  const encoded = lines.at(-1) || "";
  const json = Buffer.from(encoded, "base64").toString("utf8");
  return JSON.parse(json);
}

export function normalizeGlmOcrPayload(payload = {}) {
  const ocrResults = Array.isArray(payload.ocrResults) ? payload.ocrResults : [];
  const normalizedResults = ocrResults
    .filter((item) => item && typeof item.text === "string" && item.text.trim())
    .map((item) => ({
      text: item.text,
      rawText: item.rawText || item.text,
      regionId: item.regionId || null,
      roi: item.roi || null,
      confidence: item.confidence ?? 0.6,
      source: item.source || "glm-ocr",
    }));
  return {
    engine: payload.engine || "glm-ocr",
    text: String(payload.text || normalizedResults.map((item) => item.text).join(" ")),
    ocrResults: normalizedResults,
  };
}

function isGlmOcrUnavailable(error) {
  return /No module named ['"]?glmocr|ModuleNotFoundError|GLM-OCR is not available|Connection refused|WinError 10061|timed out/i.test(`${error?.message || ""}\n${error?.stderr || ""}`);
}

export function createGlmOcrTextExtractor({
  enabled = true,
  required = false,
  timeoutMs = 180000,
  pythonPath = pythonExecutable(),
  extraEnv = {},
  runOcr = runPythonGlmOcr,
} = {}) {
  let unavailableError = null;
  return {
    async extract(frame, { regions = [] } = {}) {
      if (!enabled || !Buffer.isBuffer(frame?.bytes)) return frame;
      if (unavailableError && !required) throw unavailableError;
      const dir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-glm-ocr-"));
      const imagePath = path.join(dir, `${randomUUID()}.png`);
      try {
        await fs.writeFile(imagePath, frame.bytes);
        const stdout = await runOcr({ imagePath, regions, timeoutMs, pythonPath, extraEnv });
        const payload = normalizeGlmOcrPayload(parseGlmOcrStdout(stdout));
        return {
          ...frame,
          text: payload.text,
          ocrResults: payload.ocrResults,
          ocrEngine: payload.engine,
        };
      } catch (error) {
        if (isGlmOcrUnavailable(error) && !required) unavailableError = error;
        throw error;
      } finally {
        await fs.rm(dir, { recursive: true, force: true }).catch(() => {});
      }
    },
  };
}

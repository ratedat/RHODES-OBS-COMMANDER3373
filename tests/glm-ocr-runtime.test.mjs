import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

import {
  GLM_OCR_RUNTIME_DIRNAME,
  glmOcrRuntimeEnv,
  probeGlmOcrRuntime,
  resolveGlmOcrRuntimePaths,
  uninstallGlmOcrRuntime,
} from "../app/domain/glm-ocr-runtime.js";

test("GLM OCR runtime paths stay under the app state directory", () => {
  const stateDir = path.join(os.tmpdir(), "rhodes-state");
  const paths = resolveGlmOcrRuntimePaths({ stateDir, platform: "win32" });

  assert.equal(paths.root, path.join(stateDir, GLM_OCR_RUNTIME_DIRNAME));
  assert.equal(paths.pythonExe, path.join(paths.venvDir, "Scripts", "python.exe"));
  assert.equal(paths.uvExe, path.join(paths.binDir, "uv.exe"));
});

test("GLM OCR runtime environment redirects caches into the runtime root", () => {
  const paths = resolveGlmOcrRuntimePaths({ stateDir: "C:/state", platform: "win32" });
  const env = glmOcrRuntimeEnv(paths, { PATH: "C:/Windows" });

  assert.equal(env.PATH, "C:/Windows");
  assert.equal(env.UV_PYTHON_INSTALL_DIR, paths.pythonInstallDir);
  assert.equal(env.UV_TOOL_DIR, paths.toolDir);
  assert.equal(env.HF_HOME.startsWith(paths.cacheDir), true);
  assert.equal(env.MODELSCOPE_CACHE.startsWith(paths.cacheDir), true);
});

test("GLM OCR runtime uninstall removes only the managed runtime directory", async () => {
  const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-glm-runtime-"));
  const paths = resolveGlmOcrRuntimePaths({ stateDir, platform: "win32" });
  const sibling = path.join(stateDir, "current-state.json");
  await fs.mkdir(paths.root, { recursive: true });
  await fs.writeFile(path.join(paths.root, "marker.txt"), "remove me", "utf8");
  await fs.writeFile(sibling, "keep me", "utf8");

  const status = await uninstallGlmOcrRuntime({ stateDir });

  await assert.rejects(fs.stat(paths.root));
  assert.equal(await fs.readFile(sibling, "utf8"), "keep me");
  assert.equal(status.status, "missing");
});

test("GLM OCR runtime probe reports missing without executing Python", async () => {
  const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-glm-runtime-missing-"));
  const status = await probeGlmOcrRuntime({ stateDir });

  assert.equal(status.status, "missing");
  assert.equal(status.installed, false);
  assert.equal(status.glmocrPresent, false);
});

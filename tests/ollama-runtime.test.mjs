import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";

import {
  OLLAMA_GLM_OCR_MODEL,
  OLLAMA_RUNTIME_DIRNAME,
  installOllamaRuntime,
  ollamaRuntimeEnv,
  probeOllamaRuntime,
  resolveInstalledOllamaRuntimeOptions,
  resolveOllamaRuntimePaths,
  uninstallOllamaRuntime,
} from "../app/domain/ollama-runtime.js";

test("Ollama runtime paths stay under the app state directory", () => {
  const stateDir = path.join(os.tmpdir(), "rhodes-state");
  const paths = resolveOllamaRuntimePaths({ stateDir, platform: "win32", arch: "x64" });

  assert.equal(paths.root, path.join(stateDir, OLLAMA_RUNTIME_DIRNAME));
  assert.equal(paths.ollamaExe, path.join(paths.installDir, "ollama.exe"));
  assert.equal(paths.modelsDir, path.join(paths.root, "models"));
  assert.equal(paths.glmOcrConfig, path.join(paths.configDir, "glm-ocr-ollama.yaml"));
});

test("Ollama runtime environment keeps models under the managed runtime", () => {
  const paths = resolveOllamaRuntimePaths({ stateDir: "C:/state", platform: "win32" });
  const env = ollamaRuntimeEnv(paths, { PATH: "C:/Windows" });

  assert.equal(env.OLLAMA_HOST, paths.host);
  assert.equal(env.OLLAMA_MODELS, paths.modelsDir);
  assert.equal(env.PATH.startsWith(`${paths.installDir}${path.delimiter}`), true);
});

test("Ollama runtime uninstall removes only the managed runtime directory", async () => {
  const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ollama-runtime-"));
  const paths = resolveOllamaRuntimePaths({ stateDir, platform: "win32" });
  const sibling = path.join(stateDir, "current-state.json");
  await fs.mkdir(paths.root, { recursive: true });
  await fs.writeFile(path.join(paths.root, "marker.txt"), "remove me", "utf8");
  await fs.writeFile(sibling, "keep me", "utf8");

  const status = await uninstallOllamaRuntime({ stateDir });

  await assert.rejects(fs.stat(paths.root));
  assert.equal(await fs.readFile(sibling, "utf8"), "keep me");
  assert.equal(status.status, "missing");
});

test("Ollama runtime probe reports missing without calling the server", async () => {
  const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ollama-runtime-missing-"));
  const status = await probeOllamaRuntime({
    stateDir,
    requestJsonImpl: async () => {
      throw new Error("should not call Ollama when executable is missing");
    },
  });

  assert.equal(status.status, "missing");
  assert.equal(status.installed, false);
  assert.equal(status.modelPresent, false);
});

test("Ollama runtime install extracts Ollama, writes GLM config, and pulls the model", async () => {
  const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ollama-runtime-install-"));
  const pulledModels = [];
  const execFileImpl = (file, args, options, callback) => {
    if (file !== "tar.exe") {
      callback(null, "", "");
      return;
    }
    const extractDir = args[args.indexOf("-C") + 1];
    fs.mkdir(path.join(extractDir, "ollama"), { recursive: true })
      .then(() => fs.writeFile(path.join(extractDir, "ollama", "ollama.exe"), "exe", "utf8"))
      .then(() => callback(null, "", ""), callback);
  };

  const status = await installOllamaRuntime({
    stateDir,
    downloadUrl: "https://example.invalid/ollama.zip",
    downloadFileImpl: async (_url, target) => fs.writeFile(target, "zip", "utf8"),
    execFileImpl,
    startServer: async () => true,
    pullModel: async ({ model }) => {
      pulledModels.push(model);
    },
    requestJsonImpl: async () => ({ models: [{ name: OLLAMA_GLM_OCR_MODEL }] }),
  });
  const paths = resolveOllamaRuntimePaths({ stateDir, platform: "win32" });
  const config = await fs.readFile(paths.glmOcrConfig, "utf8");

  assert.equal(status.status, "ready");
  assert.equal(status.modelPresent, true);
  assert.deepEqual(pulledModels, [OLLAMA_GLM_OCR_MODEL]);
  assert.equal(await fs.readFile(paths.ollamaExe, "utf8"), "exe");
  assert.match(config, /model: glm-ocr:latest/);
  assert.match(config, /api_mode: ollama_generate/);
});

test("Installed Ollama runtime exposes GLM OCR environment overrides", async () => {
  const stateDir = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-ollama-runtime-options-"));
  const paths = resolveOllamaRuntimePaths({ stateDir, platform: "win32" });
  await fs.mkdir(paths.configDir, { recursive: true });
  await fs.writeFile(paths.glmOcrConfig, "pipeline: {}\n", "utf8");

  const options = await resolveInstalledOllamaRuntimeOptions({ stateDir });

  assert.equal(options.glmOcrEnv.RHODES_GLM_OCR_CONFIG, paths.glmOcrConfig);
  assert.equal(options.glmOcrEnv.RHODES_GLM_OCR_OLLAMA_ENDPOINT, `http://${paths.host}/api/generate`);
  assert.equal(options.glmOcrEnv.RHODES_GLM_OCR_OLLAMA_MODEL, paths.model);
  assert.equal(options.glmOcrEnv.OLLAMA_MODELS, paths.modelsDir);
  assert.equal(options.glmOcrEnv.OLLAMA_HOST, paths.host);
});

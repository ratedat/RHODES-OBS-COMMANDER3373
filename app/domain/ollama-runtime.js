import { execFile, spawn } from "node:child_process";
import fs from "node:fs/promises";
import http from "node:http";
import path from "node:path";
import { downloadFile } from "./glm-ocr-runtime.js";

export const OLLAMA_RUNTIME_DIRNAME = "ollama-runtime";
export const OLLAMA_GLM_OCR_MODEL = "glm-ocr:latest";
export const OLLAMA_HOST = "127.0.0.1:11435";
export const OLLAMA_WINDOWS_AMD64_DOWNLOAD_URL = "https://github.com/ollama/ollama/releases/latest/download/ollama-windows-amd64.zip";
export const OLLAMA_WINDOWS_ARM64_DOWNLOAD_URL = "https://github.com/ollama/ollama/releases/latest/download/ollama-windows-arm64.zip";

let managedOllamaProcess = null;

function nowIso() {
  return new Date().toISOString();
}

function normalizeMessage(error) {
  return error instanceof Error ? error.message : String(error);
}

async function pathExists(target, kind = "any") {
  try {
    const stat = await fs.stat(target);
    if (kind === "file") return stat.isFile();
    if (kind === "dir") return stat.isDirectory();
    return true;
  } catch {
    return false;
  }
}

function assertChildPath(parent, child) {
  const parentPath = path.resolve(parent);
  const childPath = path.resolve(child);
  const relative = path.relative(parentPath, childPath);
  if (!relative || relative.startsWith("..") || path.isAbsolute(relative)) {
    throw new Error(`unsafe Ollama runtime path: ${childPath}`);
  }
}

function executableName(name, platform = process.platform) {
  return platform === "win32" ? `${name}.exe` : name;
}

function ollamaDownloadUrl({ platform = process.platform, arch = process.arch } = {}) {
  if (platform !== "win32") throw new Error("Ollama managed runtime is currently supported only on Windows.");
  return arch === "arm64" ? OLLAMA_WINDOWS_ARM64_DOWNLOAD_URL : OLLAMA_WINDOWS_AMD64_DOWNLOAD_URL;
}

export function resolveOllamaRuntimePaths({
  stateDir,
  runtimeDirName = OLLAMA_RUNTIME_DIRNAME,
  platform = process.platform,
  arch = process.arch,
} = {}) {
  const baseStateDir = path.resolve(stateDir || process.cwd());
  const root = path.resolve(baseStateDir, runtimeDirName);
  const downloadsDir = path.join(root, "downloads");
  const installDir = path.join(root, "app");
  const modelsDir = path.join(root, "models");
  const configDir = path.join(root, "config");
  return {
    stateDir: baseStateDir,
    root,
    downloadsDir,
    installDir,
    modelsDir,
    configDir,
    archive: path.join(downloadsDir, platform === "win32" && arch === "arm64" ? "ollama-windows-arm64.zip" : "ollama-windows-amd64.zip"),
    ollamaExe: path.join(installDir, executableName("ollama", platform)),
    glmOcrConfig: path.join(configDir, "glm-ocr-ollama.yaml"),
    host: OLLAMA_HOST,
    model: OLLAMA_GLM_OCR_MODEL,
  };
}

export function ollamaRuntimeEnv(paths, baseEnv = process.env) {
  return {
    ...baseEnv,
    PATH: [paths.installDir, baseEnv.PATH || ""].filter(Boolean).join(path.delimiter),
    OLLAMA_HOST: paths.host,
    OLLAMA_MODELS: paths.modelsDir,
  };
}

function execFileAsync(file, args, options = {}, execFileImpl = execFile) {
  return new Promise((resolve, reject) => {
    execFileImpl(file, args, {
      encoding: "utf8",
      windowsHide: true,
      maxBuffer: 64 * 1024 * 1024,
      ...options,
    }, (error, stdout, stderr) => {
      if (error) {
        error.stderr = stderr;
        reject(error);
        return;
      }
      resolve({ stdout, stderr });
    });
  });
}

async function findExecutable(root, filename, depth = 5) {
  if (depth < 0) return null;
  const entries = await fs.readdir(root, { withFileTypes: true }).catch(() => []);
  for (const entry of entries) {
    const fullPath = path.join(root, entry.name);
    if (entry.isFile() && entry.name.toLowerCase() === filename.toLowerCase()) return fullPath;
    if (entry.isDirectory()) {
      const found = await findExecutable(fullPath, filename, depth - 1);
      if (found) return found;
    }
  }
  return null;
}

async function writeGlmOcrOllamaConfig(paths) {
  await fs.mkdir(paths.configDir, { recursive: true });
  const [apiHost, apiPort = "11434"] = paths.host.split(":");
  const yaml = [
    "pipeline:",
    "  maas:",
    "    enabled: false",
    "  ocr_api:",
    `    api_host: ${apiHost}`,
    `    api_port: ${apiPort}`,
    "    api_path: /api/generate",
    `    model: ${paths.model}`,
    "    api_mode: ollama_generate",
    "",
  ].join("\n");
  await fs.writeFile(paths.glmOcrConfig, yaml, "utf8");
}

function requestJson(url, { method = "GET", body = null, timeoutMs = 5000 } = {}) {
  return new Promise((resolve, reject) => {
    const payload = body ? Buffer.from(JSON.stringify(body), "utf8") : null;
    const request = http.request(url, {
      method,
      headers: payload ? { "content-type": "application/json", "content-length": String(payload.length) } : {},
      timeout: timeoutMs,
    }, (response) => {
      const chunks = [];
      response.on("data", (chunk) => chunks.push(chunk));
      response.on("end", () => {
        const text = Buffer.concat(chunks).toString("utf8");
        if ((response.statusCode || 0) < 200 || (response.statusCode || 0) >= 300) {
          reject(new Error(`Ollama API returned HTTP ${response.statusCode}: ${text.slice(0, 400)}`));
          return;
        }
        try {
          resolve(text ? JSON.parse(text) : {});
        } catch (error) {
          reject(error);
        }
      });
    });
    request.on("error", reject);
    request.on("timeout", () => request.destroy(new Error("Ollama API request timed out")));
    if (payload) request.write(payload);
    request.end();
  });
}

function tagsUrl(paths) {
  return `http://${paths.host}/api/tags`;
}

function modelNamesFromTags(payload) {
  const models = Array.isArray(payload?.models) ? payload.models : [];
  return models.map((item) => item?.name || item?.model).filter(Boolean);
}

async function queryOllamaTags(paths, { requestJsonImpl = requestJson, timeoutMs = 2500 } = {}) {
  return requestJsonImpl(tagsUrl(paths), { timeoutMs });
}

async function waitForOllamaServer(paths, {
  requestJsonImpl = requestJson,
  timeoutMs = 30000,
  intervalMs = 600,
} = {}) {
  const startedAt = Date.now();
  let lastError = null;
  while (Date.now() - startedAt < timeoutMs) {
    try {
      await queryOllamaTags(paths, { requestJsonImpl, timeoutMs: 2500 });
      return true;
    } catch (error) {
      lastError = error;
      await new Promise((resolve) => setTimeout(resolve, intervalMs));
    }
  }
  throw new Error(`Ollama server did not become ready: ${normalizeMessage(lastError)}`);
}

async function ensureOllamaExecutable(paths, {
  downloadUrl = ollamaDownloadUrl(),
  downloadFileImpl = downloadFile,
  execFileImpl = execFile,
  onLog,
} = {}) {
  if (await pathExists(paths.ollamaExe, "file")) return;
  await fs.mkdir(paths.downloadsDir, { recursive: true });
  await fs.mkdir(paths.installDir, { recursive: true });
  const tmpArchive = `${paths.archive}.${process.pid}.tmp`;
  onLog?.({ event: "download", message: "Ollamaをダウンロードしています。", url: downloadUrl });
  await downloadFileImpl(downloadUrl, tmpArchive);
  await fs.rename(tmpArchive, paths.archive);

  const extractDir = path.join(paths.downloadsDir, "ollama-extract");
  await fs.rm(extractDir, { recursive: true, force: true });
  await fs.mkdir(extractDir, { recursive: true });
  onLog?.({ event: "extract", message: "Ollamaを展開しています。" });
  await execFileAsync("tar.exe", ["-xf", paths.archive, "-C", extractDir], { timeout: 10 * 60 * 1000 }, execFileImpl);
  const ollamaExe = await findExecutable(extractDir, path.basename(paths.ollamaExe));
  if (!ollamaExe) throw new Error("ollama.exe was not found in the downloaded archive.");
  await fs.rm(paths.installDir, { recursive: true, force: true });
  await fs.mkdir(paths.installDir, { recursive: true });
  await fs.cp(path.dirname(ollamaExe), paths.installDir, { recursive: true });
}

export async function startOllamaRuntime({
  stateDir,
  spawnImpl = spawn,
  requestJsonImpl = requestJson,
  onLog,
} = {}) {
  const paths = resolveOllamaRuntimePaths({ stateDir });
  if (!(await pathExists(paths.ollamaExe, "file"))) throw new Error("Ollama is not installed.");
  try {
    await queryOllamaTags(paths, { requestJsonImpl, timeoutMs: 1500 });
    return true;
  } catch {
    // Start the managed server below.
  }
  if (!managedOllamaProcess || managedOllamaProcess.killed) {
    onLog?.({ event: "serve", message: "Ollamaサーバーを起動しています。" });
    managedOllamaProcess = spawnImpl(paths.ollamaExe, ["serve"], {
      cwd: paths.installDir,
      env: ollamaRuntimeEnv(paths),
      windowsHide: true,
      stdio: "ignore",
    });
    managedOllamaProcess.unref?.();
  }
  await waitForOllamaServer(paths, { requestJsonImpl });
  return true;
}

export async function pullOllamaModel({
  stateDir,
  model = OLLAMA_GLM_OCR_MODEL,
  execFileImpl = execFile,
  onLog,
} = {}) {
  const paths = resolveOllamaRuntimePaths({ stateDir });
  onLog?.({ event: "pull", message: `Ollamaモデル ${model} を取得しています。` });
  const { stdout, stderr } = await execFileAsync(paths.ollamaExe, ["pull", model], {
    cwd: paths.installDir,
    env: ollamaRuntimeEnv(paths),
    timeout: 4 * 60 * 60 * 1000,
  }, execFileImpl);
  const output = [stdout, stderr].filter(Boolean).join("\n").trim();
  if (output) onLog?.({ event: "pull_output", message: output.slice(-4000) });
}

export async function probeOllamaRuntime({ stateDir, requestJsonImpl = requestJson } = {}) {
  const paths = resolveOllamaRuntimePaths({ stateDir });
  const executablePresent = await pathExists(paths.ollamaExe, "file");
  const configPresent = await pathExists(paths.glmOcrConfig, "file");
  const modelsDirPresent = await pathExists(paths.modelsDir, "dir");
  let serverReachable = false;
  let modelPresent = false;
  let probeError = null;
  if (executablePresent) {
    try {
      const tags = await queryOllamaTags(paths, { requestJsonImpl });
      serverReachable = true;
      modelPresent = modelNamesFromTags(tags).includes(paths.model);
    } catch (error) {
      probeError = normalizeMessage(error);
    }
  }
  const installed = executablePresent;
  const status = executablePresent && configPresent && serverReachable && modelPresent
    ? "ready"
    : executablePresent || configPresent || modelsDirPresent
      ? "partial"
      : "missing";
  const offlineInstalled = executablePresent && configPresent && modelsDirPresent && !serverReachable;
  return {
    status,
    installed,
    installing: false,
    executablePresent,
    configPresent,
    modelsDirPresent,
    serverReachable,
    modelPresent,
    installRoot: paths.root,
    executablePath: paths.ollamaExe,
    model: paths.model,
    modelsDir: paths.modelsDir,
    configPath: paths.glmOcrConfig,
    message: status === "ready"
      ? "Ollama + GLM-OCRモデルを使用できます。"
      : status === "partial"
        ? offlineInstalled
          ? "Ollamaは導入済みですが未起動です。起動を実行してください。"
          : serverReachable
          ? "Ollamaは起動していますが、GLM-OCRモデルまたは設定が未完了です。"
          : "Ollamaは未完了または未起動です。導入/起動を実行してください。"
        : "Ollamaローカル実行は未導入です。",
    ...(probeError ? { probeError } : {}),
  };
}

export async function installOllamaRuntime({
  stateDir,
  downloadUrl = ollamaDownloadUrl(),
  downloadFileImpl = downloadFile,
  execFileImpl = execFile,
  requestJsonImpl = requestJson,
  startServer = startOllamaRuntime,
  pullModel = pullOllamaModel,
  onLog,
} = {}) {
  const paths = resolveOllamaRuntimePaths({ stateDir });
  assertChildPath(paths.stateDir, paths.root);
  await fs.mkdir(paths.root, { recursive: true });
  await fs.mkdir(paths.modelsDir, { recursive: true });
  await ensureOllamaExecutable(paths, { downloadUrl, downloadFileImpl, execFileImpl, onLog });
  await writeGlmOcrOllamaConfig(paths);
  await startServer({ stateDir, requestJsonImpl, onLog });
  await pullModel({ stateDir, model: paths.model, execFileImpl, onLog });
  return probeOllamaRuntime({ stateDir, requestJsonImpl });
}

export async function uninstallOllamaRuntime({ stateDir } = {}) {
  const paths = resolveOllamaRuntimePaths({ stateDir });
  assertChildPath(paths.stateDir, paths.root);
  if (managedOllamaProcess && !managedOllamaProcess.killed) {
    managedOllamaProcess.kill();
    managedOllamaProcess = null;
  }
  await fs.rm(paths.root, { recursive: true, force: true });
  return probeOllamaRuntime({ stateDir });
}

export async function resolveInstalledOllamaRuntimeOptions({ stateDir } = {}) {
  const paths = resolveOllamaRuntimePaths({ stateDir });
  if (!(await pathExists(paths.glmOcrConfig, "file"))) return {};
  return {
    glmOcrEnv: {
      RHODES_GLM_OCR_CONFIG: paths.glmOcrConfig,
      RHODES_GLM_OCR_OLLAMA_ENDPOINT: `http://${paths.host}/api/generate`,
      RHODES_GLM_OCR_OLLAMA_MODEL: paths.model,
      OLLAMA_HOST: paths.host,
      OLLAMA_MODELS: paths.modelsDir,
    },
  };
}

export function createOllamaRuntimeManager({
  stateDir,
  installRuntime = installOllamaRuntime,
  uninstallRuntime = uninstallOllamaRuntime,
  probeRuntime = probeOllamaRuntime,
  startRuntime = startOllamaRuntime,
} = {}) {
  let installPromise = null;
  let job = null;
  const pushLog = (entry) => {
    if (!job) return;
    job.log.push({ at: nowIso(), ...entry });
    job.log = job.log.slice(-30);
    job.updatedAt = nowIso();
  };
  const withJobState = async (base) => ({
    ...base,
    installing: Boolean(installPromise),
    installJob: job,
  });

  return {
    async status() {
      const base = await probeRuntime({ stateDir });
      return withJobState(base);
    },
    async install() {
      if (installPromise) return this.status();
      job = {
        status: "installing",
        startedAt: nowIso(),
        updatedAt: nowIso(),
        completedAt: null,
        error: null,
        log: [],
      };
      installPromise = (async () => {
        try {
          pushLog({ event: "start", message: "Ollama + GLM-OCRモデルの導入を開始しました。" });
          await installRuntime({ stateDir, onLog: pushLog });
          job.status = "ready";
          job.completedAt = nowIso();
          pushLog({ event: "complete", message: "Ollama + GLM-OCRモデルの導入が完了しました。" });
        } catch (error) {
          job.status = "failed";
          job.error = normalizeMessage(error);
          job.completedAt = nowIso();
          pushLog({ event: "failed", message: job.error });
        } finally {
          installPromise = null;
        }
      })();
      return this.status();
    },
    async start() {
      if (installPromise) return this.status();
      job = {
        status: "starting",
        startedAt: nowIso(),
        updatedAt: nowIso(),
        completedAt: null,
        error: null,
        log: [],
      };
      try {
        await startRuntime({ stateDir, onLog: pushLog });
        job.status = "ready";
        job.completedAt = nowIso();
        pushLog({ event: "complete", message: "Ollamaサーバーを起動しました。" });
      } catch (error) {
        job.status = "failed";
        job.error = normalizeMessage(error);
        job.completedAt = nowIso();
        pushLog({ event: "failed", message: job.error });
      }
      return this.status();
    },
    async uninstall() {
      if (installPromise) throw Object.assign(new Error("Ollama runtime install is still running."), { status: 409 });
      job = null;
      await uninstallRuntime({ stateDir });
      return this.status();
    },
  };
}

import { execFile } from "node:child_process";
import { createWriteStream } from "node:fs";
import fs from "node:fs/promises";
import https from "node:https";
import http from "node:http";
import path from "node:path";

export const GLM_OCR_RUNTIME_DIRNAME = "glm-ocr-runtime";
export const GLM_OCR_PYTHON_VERSION = "3.12";
export const GLM_OCR_PACKAGE_SPEC = "glmocr[selfhosted,server]";
export const UV_WINDOWS_DOWNLOAD_URL = "https://github.com/astral-sh/uv/releases/latest/download/uv-x86_64-pc-windows-msvc.zip";

const GLM_OCR_PROBE_SCRIPT = [
  "import importlib.util, json, sys",
  "print(json.dumps({",
  "  'python': sys.executable,",
  "  'glmocr': importlib.util.find_spec('glmocr') is not None,",
  "}, ensure_ascii=False))",
].join("\n");

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
    throw new Error(`unsafe GLM-OCR runtime path: ${childPath}`);
  }
}

function runtimeScriptDir(platform = process.platform) {
  return platform === "win32" ? "Scripts" : "bin";
}

function executableName(name, platform = process.platform) {
  return platform === "win32" ? `${name}.exe` : name;
}

export function resolveGlmOcrRuntimePaths({ stateDir, runtimeDirName = GLM_OCR_RUNTIME_DIRNAME, platform = process.platform } = {}) {
  const baseStateDir = path.resolve(stateDir || process.cwd());
  const root = path.resolve(baseStateDir, runtimeDirName);
  const binDir = path.join(root, "bin");
  const downloadsDir = path.join(root, "downloads");
  const cacheDir = path.join(root, "cache");
  const venvDir = path.join(root, "venv");
  const toolDir = path.join(root, "tools");
  const pythonInstallDir = path.join(root, "python");
  const scriptDir = runtimeScriptDir(platform);
  return {
    stateDir: baseStateDir,
    root,
    binDir,
    downloadsDir,
    cacheDir,
    venvDir,
    toolDir,
    pythonInstallDir,
    uvArchive: path.join(downloadsDir, "uv-x86_64-pc-windows-msvc.zip"),
    uvExe: path.join(binDir, executableName("uv", platform)),
    pythonExe: path.join(venvDir, scriptDir, executableName("python", platform)),
  };
}

export function glmOcrRuntimeEnv(paths, baseEnv = process.env) {
  const cacheDir = paths.cacheDir;
  const hfHome = path.join(cacheDir, "huggingface");
  return {
    ...baseEnv,
    UV_CACHE_DIR: path.join(cacheDir, "uv"),
    UV_PYTHON_INSTALL_DIR: paths.pythonInstallDir,
    UV_TOOL_DIR: paths.toolDir,
    UV_NO_MODIFY_PATH: "1",
    UV_PYTHON_PREFERENCE: "only-managed",
    XDG_CACHE_HOME: cacheDir,
    HF_HOME: hfHome,
    HUGGINGFACE_HUB_CACHE: path.join(hfHome, "hub"),
    MODELSCOPE_CACHE: path.join(cacheDir, "modelscope"),
    MPLCONFIGDIR: path.join(cacheDir, "matplotlib"),
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

function requestClient(url) {
  return String(url).startsWith("http://") ? http : https;
}

export function downloadFile(url, target, { maxRedirects = 5 } = {}) {
  return new Promise((resolve, reject) => {
    const run = (nextUrl, redirectsLeft) => {
      const request = requestClient(nextUrl).get(nextUrl, (response) => {
        const status = Number(response.statusCode || 0);
        const location = response.headers.location;
        if (status >= 300 && status < 400 && location && redirectsLeft > 0) {
          response.resume();
          run(new URL(location, nextUrl).toString(), redirectsLeft - 1);
          return;
        }
        if (status !== 200) {
          response.resume();
          reject(new Error(`download failed: HTTP ${status}`));
          return;
        }
        const stream = createWriteStream(target);
        response.pipe(stream);
        stream.on("finish", () => stream.close(resolve));
        stream.on("error", reject);
      });
      request.on("error", reject);
      request.setTimeout(120000, () => {
        request.destroy(new Error("download timed out"));
      });
    };
    run(url, maxRedirects);
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

async function ensureUvExecutable(paths, {
  downloadUrl = UV_WINDOWS_DOWNLOAD_URL,
  downloadFileImpl = downloadFile,
  execFileImpl = execFile,
  onLog,
} = {}) {
  if (await pathExists(paths.uvExe, "file")) return;
  await fs.mkdir(paths.downloadsDir, { recursive: true });
  await fs.mkdir(paths.binDir, { recursive: true });
  const tmpArchive = `${paths.uvArchive}.${process.pid}.tmp`;
  onLog?.({ event: "download", message: "uvをダウンロードしています。", url: downloadUrl });
  await downloadFileImpl(downloadUrl, tmpArchive);
  await fs.rename(tmpArchive, paths.uvArchive);

  const extractDir = path.join(paths.downloadsDir, "uv-extract");
  await fs.rm(extractDir, { recursive: true, force: true });
  await fs.mkdir(extractDir, { recursive: true });
  onLog?.({ event: "extract", message: "uvを展開しています。" });
  await execFileAsync("tar.exe", ["-xf", paths.uvArchive, "-C", extractDir], { timeout: 120000 }, execFileImpl);
  const uvExe = await findExecutable(extractDir, path.basename(paths.uvExe));
  if (!uvExe) throw new Error("uv.exe was not found in the downloaded archive.");
  await fs.copyFile(uvExe, paths.uvExe);
}

async function runInstallCommand(paths, args, {
  execFileImpl = execFile,
  onLog,
  timeoutMs = 15 * 60 * 1000,
} = {}) {
  onLog?.({ event: "command", message: `uv ${args.join(" ")}` });
  const { stdout, stderr } = await execFileAsync(paths.uvExe, args, {
    cwd: paths.root,
    env: glmOcrRuntimeEnv(paths),
    timeout: timeoutMs,
  }, execFileImpl);
  const output = [stdout, stderr].filter(Boolean).join("\n").trim();
  if (output) onLog?.({ event: "command_output", message: output.slice(-4000) });
}

export async function probeGlmOcrRuntime({ stateDir, execFileImpl = execFile } = {}) {
  const paths = resolveGlmOcrRuntimePaths({ stateDir });
  const uvPresent = await pathExists(paths.uvExe, "file");
  const pythonPresent = await pathExists(paths.pythonExe, "file");
  let probe = null;
  let probeError = null;
  if (pythonPresent) {
    try {
      const { stdout } = await execFileAsync(paths.pythonExe, ["-c", GLM_OCR_PROBE_SCRIPT], {
        env: glmOcrRuntimeEnv(paths),
        timeout: 30000,
      }, execFileImpl);
      probe = JSON.parse(String(stdout || "{}"));
    } catch (error) {
      probeError = normalizeMessage(error);
    }
  }
  const glmocrPresent = Boolean(probe?.glmocr);
  const status = glmocrPresent ? "ready" : pythonPresent || uvPresent ? "partial" : "missing";
  return {
    status,
    installed: glmocrPresent,
    installing: false,
    uvPresent,
    pythonPresent,
    glmocrPresent,
    installRoot: paths.root,
    uvPath: paths.uvExe,
    pythonPath: paths.pythonExe,
    cacheDir: paths.cacheDir,
    message: status === "ready"
      ? "GLM-OCRを使用できます。"
      : status === "partial"
        ? "GLM-OCRランタイムは未完了です。インストールを再実行してください。"
        : "GLM-OCRランタイムは未導入です。",
    ...(probeError ? { probeError } : {}),
  };
}

export async function installGlmOcrRuntime({
  stateDir,
  packageSpec = GLM_OCR_PACKAGE_SPEC,
  downloadUrl = UV_WINDOWS_DOWNLOAD_URL,
  downloadFileImpl = downloadFile,
  execFileImpl = execFile,
  onLog,
} = {}) {
  const paths = resolveGlmOcrRuntimePaths({ stateDir });
  assertChildPath(paths.stateDir, paths.root);
  await fs.mkdir(paths.root, { recursive: true });
  await fs.mkdir(paths.cacheDir, { recursive: true });
  await ensureUvExecutable(paths, { downloadUrl, downloadFileImpl, execFileImpl, onLog });
  await runInstallCommand(paths, ["python", "install", GLM_OCR_PYTHON_VERSION], { execFileImpl, onLog });
  await runInstallCommand(paths, ["venv", paths.venvDir, "--python", GLM_OCR_PYTHON_VERSION, "--seed"], { execFileImpl, onLog });
  await runInstallCommand(paths, ["pip", "install", "--python", paths.pythonExe, "--upgrade", "pip"], { execFileImpl, onLog });
  await runInstallCommand(paths, ["pip", "install", "--python", paths.pythonExe, packageSpec], { execFileImpl, onLog, timeoutMs: 45 * 60 * 1000 });
  return probeGlmOcrRuntime({ stateDir, execFileImpl });
}

export async function uninstallGlmOcrRuntime({ stateDir } = {}) {
  const paths = resolveGlmOcrRuntimePaths({ stateDir });
  assertChildPath(paths.stateDir, paths.root);
  await fs.rm(paths.root, { recursive: true, force: true });
  return probeGlmOcrRuntime({ stateDir });
}

export async function resolveInstalledGlmOcrRuntimeOptions({ stateDir } = {}) {
  const paths = resolveGlmOcrRuntimePaths({ stateDir });
  if (!(await pathExists(paths.pythonExe, "file"))) return {};
  return {
    glmOcrPythonPath: paths.pythonExe,
    glmOcrEnv: glmOcrRuntimeEnv(paths),
  };
}

export function createGlmOcrRuntimeManager({
  stateDir,
  installRuntime = installGlmOcrRuntime,
  uninstallRuntime = uninstallGlmOcrRuntime,
  probeRuntime = probeGlmOcrRuntime,
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
          pushLog({ event: "start", message: "GLM-OCRランタイムのインストールを開始しました。" });
          await installRuntime({ stateDir, onLog: pushLog });
          job.status = "ready";
          job.completedAt = nowIso();
          pushLog({ event: "complete", message: "GLM-OCRランタイムのインストールが完了しました。" });
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
    async uninstall() {
      if (installPromise) throw Object.assign(new Error("GLM-OCR runtime install is still running."), { status: 409 });
      job = null;
      await uninstallRuntime({ stateDir });
      return this.status();
    },
  };
}

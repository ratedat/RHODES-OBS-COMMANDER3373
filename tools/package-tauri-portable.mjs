import { execFile } from "node:child_process";
import { access, cp, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { promisify } from "node:util";

const execFileAsync = promisify(execFile);

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(__dirname, "..");
const releaseDir = path.join(root, "outputs", "release");
const releaseExe = path.join(root, "src-tauri", "target", "release", "rhodes-obs-commander3373-tauri.exe");
const releaseResources = path.join(root, "src-tauri", "target", "release", "resources");
const preparedResources = path.join(root, "src-tauri", "resources");
const portableExeName = "RHODES OBS COMMANDER3373.exe";

function stamp() {
  const date = new Date();
  const pad = (value) => String(value).padStart(2, "0");
  return [
    date.getFullYear(),
    pad(date.getMonth() + 1),
    pad(date.getDate()),
    "-",
    pad(date.getHours()),
    pad(date.getMinutes()),
    pad(date.getSeconds()),
  ].join("");
}

function assertChildPath(parent, child) {
  const relative = path.relative(parent, child);
  if (!relative || relative.startsWith("..") || path.isAbsolute(relative)) {
    throw new Error(`refusing to operate outside ${parent}: ${child}`);
  }
}

async function exists(file) {
  try {
    await access(file);
    return true;
  } catch {
    return false;
  }
}

async function gitShortSha() {
  try {
    const { stdout } = await execFileAsync("git", ["rev-parse", "--short", "HEAD"], { cwd: root, windowsHide: true });
    const { stdout: status } = await execFileAsync("git", ["status", "--porcelain"], { cwd: root, windowsHide: true });
    return `${stdout.trim()}${status.trim() ? "-dirty" : ""}`;
  } catch {
    return "nogit";
  }
}

async function resourceSource() {
  if (await exists(path.join(releaseResources, "rhodes-app", "app", "server.mjs"))) return releaseResources;
  if (await exists(path.join(preparedResources, "rhodes-app", "app", "server.mjs"))) return preparedResources;
  throw new Error("Tauri resources not found. Run npm run tauri:build first.");
}

async function zipDirectory(folder, zipPath) {
  await rm(zipPath, { force: true });
  try {
    await execFileAsync("tar.exe", ["-a", "-cf", zipPath, "-C", path.dirname(folder), path.basename(folder)], {
      cwd: root,
      timeout: 120000,
      windowsHide: true,
    });
    return "tar.exe";
  } catch (tarError) {
    try {
      await execFileAsync("powershell.exe", [
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-Command",
        "Compress-Archive -LiteralPath $args[0] -DestinationPath $args[1] -Force",
        folder,
        zipPath,
      ], {
        cwd: root,
        timeout: 120000,
        windowsHide: true,
      });
      return "Compress-Archive";
    } catch (zipError) {
      zipError.message = `failed to create zip with tar.exe or Compress-Archive: ${zipError.message}; tar: ${tarError.message}`;
      throw zipError;
    }
  }
}

async function writePortableReadme(packageDir) {
  await writeFile(path.join(packageDir, "README-PORTABLE.txt"), [
    "RHODES OBS COMMANDER3373 ポータブル版",
    "",
    "使い方:",
    "1. ZIPを好きな場所へ展開します。",
    `2. ${portableExeName} を起動します。`,
    "3. 設定やログは同じフォルダ内の RHODES OBS COMMANDER3373 Data に作成されます。",
    "",
    "注意:",
    "- resources フォルダは削除しないでください。アプリ本体が使用します。",
    "- GLM-OCR と Ollama は同梱していません。必要な場合だけアプリ内から導入してください。",
    "- Windows Defender SmartScreen が出る場合があります。コード署名前の個人配布では避けにくい警告です。",
    "",
  ].join("\r\n"), "utf8");
}

async function main() {
  const pkg = JSON.parse(await readFile(path.join(root, "package.json"), "utf8"));
  const sha = await gitShortSha();
  const packageDirName = `RHODES-OBS-COMMANDER3373-tauri-portable-${pkg.version}-${stamp()}-${sha}`;
  const packageDir = path.join(releaseDir, packageDirName);
  const zipPath = `${packageDir}.zip`;
  assertChildPath(releaseDir, packageDir);
  assertChildPath(releaseDir, zipPath);

  if (!(await exists(releaseExe))) {
    throw new Error(`Tauri release executable not found: ${releaseExe}. Run npm run tauri:build first.`);
  }
  const resources = await resourceSource();

  await mkdir(releaseDir, { recursive: true });
  await rm(packageDir, { recursive: true, force: true });
  await mkdir(packageDir, { recursive: true });

  await cp(releaseExe, path.join(packageDir, portableExeName), { force: true });
  await cp(resources, path.join(packageDir, "resources"), { recursive: true, force: true });
  for (const file of ["LICENSE", "README.md", "THIRD_PARTY_NOTICES.md"]) {
    await cp(path.join(root, file), path.join(packageDir, file), { force: true });
  }
  await writePortableReadme(packageDir);

  const zipTool = await zipDirectory(packageDir, zipPath);
  console.log(JSON.stringify({
    ok: true,
    packageDir,
    zipPath,
    exe: path.join(packageDir, portableExeName),
    resources,
    zipTool,
  }, null, 2));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});

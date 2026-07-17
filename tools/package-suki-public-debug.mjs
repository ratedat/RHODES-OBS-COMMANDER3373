import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const portableRoot = path.join(repoRoot, "outputs", "suki-portable");
const releaseRoot = path.join(repoRoot, "outputs", "release");
const excludedPortableEntries = new Set([
  "user-data",
  "RHODES OBS COMMANDER3373 Debug Logs",
  "glm-ocr-runtime",
  "ollama-runtime",
]);

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: repoRoot,
    encoding: options.capture ? "utf8" : undefined,
    stdio: options.capture ? ["ignore", "pipe", "pipe"] : "inherit",
  });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    const detail = options.capture ? String(result.stderr || result.stdout || "").trim() : "";
    throw new Error(`${command} ${args.join(" ")} failed with exit code ${result.status}${detail ? `: ${detail}` : ""}`);
  }
  return options.capture ? String(result.stdout || "").trim() : "";
}

function buildTimestamp(date = new Date()) {
  const part = (value) => String(value).padStart(2, "0");
  return [
    date.getFullYear(),
    part(date.getMonth() + 1),
    part(date.getDate()),
    "-",
    part(date.getHours()),
    part(date.getMinutes()),
    part(date.getSeconds()),
  ].join("");
}

async function copyFile(source, target) {
  await fs.mkdir(path.dirname(target), { recursive: true });
  await fs.copyFile(source, target);
}

async function copyPortablePayload(targetRoot) {
  await fs.cp(portableRoot, targetRoot, {
    recursive: true,
    filter(source) {
      const relative = path.relative(portableRoot, source);
      if (!relative || relative.startsWith("..")) return true;
      const [topLevel] = relative.split(path.sep);
      return !excludedPortableEntries.has(topLevel);
    },
  });
}

async function addWebOverlayRuntime(targetRoot) {
  await fs.cp(path.join(repoRoot, "app"), path.join(targetRoot, "app"), { recursive: true });
  await copyFile(path.join(repoRoot, "package.json"), path.join(targetRoot, "package.json"));
  await copyFile(
    path.join(repoRoot, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes-generated.json"),
    path.join(targetRoot, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes-generated.json"),
  );
}

async function addPublicDocuments(targetRoot, sourceRevision, sourceStatus) {
  const copies = [
    ["LICENSE", "LICENSE"],
    ["THIRD_PARTY_NOTICES.md", "THIRD_PARTY_NOTICES.md"],
    ["docs/adb-setup.md", "docs/adb-setup.md"],
    ["docs/debugger-adb-report-guide.md", "docs/debugger-adb-report-guide.md"],
    ["docs/sarkaz-test-guide.md", "docs/sarkaz-test-guide.md"],
  ];
  for (const [source, target] of copies) {
    await copyFile(path.join(repoRoot, source), path.join(targetRoot, target));
  }

  const readme = `# RHODES OBS COMMANDER3373 公開デバッグ版

この配布はIS#5「サルカズの炉辺奇談」を優先したAvalonia/SukiUI公開デバッグ版です。

## 起動

1. ZIPをすべて展開します。ZIP内から直接起動しないでください。
2. \`RhodesSuki.exe\` を実行します。
3. 「ランタイム」でADBを自動検出し、「接続・撮影」で1280x720の画像が取得できることを確認します。
4. 上部の「ADB取得・認識・反映」または各認識画面を実行します。

## 不具合報告

「デバッグ」→「バグ報告ZIP」→「作成」を押し、生成されたZIPを報告へ添付してください。
過去ログ、個人設定、GLM/Ollama本体やモデルはこの配布ZIPに含めていません。

## OBS Overlay / Sidecar

OBS出力を使う場合はNode.js 22以降が別途必要です。「出力」画面で配信サーバーを起動し、Overlay URLをOBSブラウザソースへ追加します。
Web表示ソースは配布内の \`app/server.mjs\` に同梱しています。ADB/OCR検証だけならNode.jsは不要です。

## 対象と制約

- 公開デバッグの認識対象はIS#5サルカズを優先します。
- 基準解像度は1280x720 (16:9) です。
- OCRはMAA-OCRが既定です。GLM/Ollamaは任意導入で、このZIPには含まれません。
- Android Back keyeventは使いません。タップとスワイプは指定矩形内でランダム化されます。

詳細は \`docs/sarkaz-test-guide.md\`、\`docs/debugger-adb-report-guide.md\`、\`docs/adb-setup.md\` を参照してください。

Source: https://github.com/ratedat/RHODES-OBS-COMMANDER3373
Revision: ${sourceRevision}${sourceStatus ? ` (${sourceStatus})` : ""}
`;
  await fs.writeFile(path.join(targetRoot, "README_PUBLIC_DEBUG.md"), readme, "utf8");
  await fs.writeFile(
    path.join(targetRoot, "BUILD_INFO.txt"),
    `revision=${sourceRevision}\nstatus=${sourceStatus || "clean"}\nbuiltAt=${new Date().toISOString()}\n`,
    "utf8",
  );
}

async function resetPublicState(targetRoot) {
  const statePath = path.join(targetRoot, "data", "current-state.json");
  const state = JSON.parse(await fs.readFile(statePath, "utf8"));
  state.mode = "casual";
  state.run = {
    ...(state.run || {}),
    campaignId: "is5_sarkaz",
    squadId: null,
    squad: null,
    squadRandomEffectOptionId: null,
    performanceId: null,
    difficulty: null,
    difficultyTierId: null,
    ingot: null,
    special: {
      ...((state.run && state.run.special) || {}),
      is5_sarkaz: {
        thought: [],
        age: null,
        thoughtOverlayVisible: false,
        idea: 0,
      },
    },
  };
  state.relics = [];
  state.operators = [];
  state.bossFlags = [];
  state.pendingSuggestions = [];
  state.updatedAt = null;
  state.tournament = { pendingState: null, lastSubmissionAt: null, submittedBy: null };
  state.adb = {
    autoDetect: true,
    connectionPreset: "auto",
    adbPath: "",
    serial: "",
    emulatorPath: "",
    screenshotExtension: true,
    restartServerOnFailure: true,
    restartProcessOnFailure: true,
    closeAdbOnExit: false,
    lightweightAdb: false,
    reconnectAttempts: 5,
    reconnectDelayMs: 1000,
  };
  await fs.writeFile(statePath, `${JSON.stringify(state, null, 2)}\n`, "utf8");
  await copyFile(
    path.join(repoRoot, "data", "overlay-state.example.json"),
    path.join(targetRoot, "data", "overlay-state.example.json"),
  );
}

async function sha256(filePath) {
  const hash = createHash("sha256");
  const bytes = await fs.readFile(filePath);
  hash.update(bytes);
  return hash.digest("hex").toUpperCase();
}

await fs.access(path.join(portableRoot, "RhodesSuki.exe"));
const revision = run("git", ["rev-parse", "--short", "HEAD"], { capture: true });
const dirty = run("git", ["status", "--porcelain"], { capture: true }).length > 0;
const sourceStatus = dirty ? "dirty working tree" : "clean";
const packageName = `RHODES-OBS-COMMANDER3373-public-debug-${buildTimestamp()}-${revision}${dirty ? "-dirty" : ""}`;
const packageRoot = path.join(releaseRoot, packageName);
const zipPath = path.join(releaseRoot, `${packageName}.zip`);

await fs.mkdir(releaseRoot, { recursive: true });
await fs.rm(packageRoot, { recursive: true, force: true });
await fs.rm(zipPath, { force: true });
await copyPortablePayload(packageRoot);
await addWebOverlayRuntime(packageRoot);
await resetPublicState(packageRoot);
await addPublicDocuments(packageRoot, revision, sourceStatus);

run("tar.exe", ["-a", "-c", "-f", zipPath, "-C", releaseRoot, packageName]);
const archiveHash = await sha256(zipPath);
const archiveSizeMb = Math.round(((await fs.stat(zipPath)).size / 1024 / 1024) * 10) / 10;
console.log(`Public debug folder: ${path.relative(repoRoot, packageRoot)}`);
console.log(`Public debug ZIP: ${path.relative(repoRoot, zipPath)} (${archiveSizeMb} MB)`);
console.log(`SHA256: ${archiveHash}`);

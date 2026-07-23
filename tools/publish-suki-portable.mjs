import { spawnSync } from "node:child_process";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const outputsRoot = path.join(repoRoot, "outputs");
const outputDir = path.join(outputsRoot, "suki-portable");
const sukiBuildDir = path.join(repoRoot, "apps", "rhodes-suki", "bin", "Release", "net8.0", "win-x64");
const preservedTopLevelEntries = new Set([
  "user-data",
  "RHODES OBS COMMANDER3373 Debug Logs",
  "glm-ocr-runtime",
  "ollama-runtime",
  "nodejs-runtime",
]);

function assertSafeOutputPath() {
  const relative = path.relative(outputsRoot, outputDir);
  if (relative.startsWith("..") || path.isAbsolute(relative) || relative.length === 0) {
    throw new Error(`Refusing to clean unexpected output path: ${outputDir}`);
  }
}

function run(command, args) {
  const executable = process.platform === "win32" && command === "npm" ? "npm.cmd" : command;
  const result = spawnSync(executable, args, {
    cwd: repoRoot,
    stdio: "inherit",
  });
  if (result.error) throw result.error;
  if (result.status !== 0) {
    throw new Error(`${command} ${args.join(" ")} failed with exit code ${result.status}`);
  }
}

async function removeFilesByExtension(directory, extension) {
  const entries = await fs.readdir(directory, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      await removeFilesByExtension(fullPath, extension);
      continue;
    }
    if (entry.isFile() && path.extname(entry.name).toLowerCase() === extension) {
      await fs.rm(fullPath, { force: true });
    }
  }
}

async function cleanOutputPreservingUserData() {
  await fs.mkdir(outputDir, { recursive: true });
  const entries = await fs.readdir(outputDir, { withFileTypes: true });
  for (const entry of entries) {
    if (preservedTopLevelEntries.has(entry.name)) continue;
    await fs.rm(path.join(outputDir, entry.name), { recursive: true, force: true });
  }
}

async function moveMaaAgentBinaryToLibs() {
  const source = path.join(outputDir, "MaaAgentBinary");
  const target = path.join(outputDir, "libs", "MaaAgentBinary");
  try {
    const sourceStat = await fs.stat(source);
    if (!sourceStat.isDirectory()) return;
  } catch {
    return;
  }

  await fs.mkdir(path.dirname(target), { recursive: true });
  await fs.rm(target, { recursive: true, force: true });
  await fs.rename(source, target);
}

async function copyMaaNativeRuntimeToRuntimes() {
  const source = path.join(sukiBuildDir, "runtimes", "win-x64", "native");
  const target = path.join(outputDir, "runtimes", "win-x64", "native");
  const requiredFiles = [
    "MaaFramework.dll",
    "MaaToolkit.dll",
    "MaaUtils.dll",
    "MaaAdbControlUnit.dll",
    "fastdeploy_ppocr_maa.dll",
    "onnxruntime_maa.dll",
    "opencv_world4_maa.dll",
  ];

  try {
    const sourceStat = await fs.stat(source);
    if (!sourceStat.isDirectory()) {
      throw new Error(`MAA native runtime source is not a directory: ${source}`);
    }
  } catch (error) {
    throw new Error(`MAA native runtime source was not found: ${source}`, { cause: error });
  }

  await fs.rm(target, { recursive: true, force: true });
  await fs.mkdir(target, { recursive: true });
  await fs.cp(source, target, { recursive: true });

  for (const fileName of requiredFiles) {
    await fs.access(path.join(target, fileName));
  }
}

async function copyRequiredMasterData() {
  const requiredFiles = [
    "data/campaigns.json",
    "data/operators.json",
    "data/performances.json",
    "data/relics.json",
    "data/selectable-effects.json",
    "data/recognition/maa-operator-name-ocr.json",
  ];

  for (const relativePath of requiredFiles) {
    const source = path.join(repoRoot, relativePath);
    const target = path.join(outputDir, relativePath);
    await fs.access(source);
    await fs.mkdir(path.dirname(target), { recursive: true });
    await fs.copyFile(source, target);
    await fs.access(target);
  }
}

async function copyWebOverlayRuntime() {
  const source = path.join(repoRoot, "app");
  const target = path.join(outputDir, "app");
  await fs.rm(target, { recursive: true, force: true });
  await fs.cp(source, target, { recursive: true });
  await fs.copyFile(path.join(repoRoot, "package.json"), path.join(outputDir, "package.json"));
  await fs.access(path.join(target, "server.mjs"));
}

async function copyWebOverlayAssets() {
  const assetDirectories = ["bosses", "performances", "selectable-effects"];
  for (const directory of assetDirectories) {
    const source = path.join(repoRoot, "assets", directory);
    const target = path.join(outputDir, "assets", directory);
    await fs.access(source);
    await fs.rm(target, { recursive: true, force: true });
    await fs.mkdir(path.dirname(target), { recursive: true });
    await fs.cp(source, target, { recursive: true });
  }

  const dataFiles = ["campaigns.json", "performances.json", "selectable-effects.json"];
  const referencedAssets = new Set();
  const visit = (value) => {
    if (Array.isArray(value)) {
      value.forEach(visit);
      return;
    }
    if (!value || typeof value !== "object") return;
    if (typeof value.localPath === "string" && value.localPath.startsWith("assets/")) {
      referencedAssets.add(value.localPath);
    }
    Object.values(value).forEach(visit);
  };
  for (const fileName of dataFiles) {
    const data = JSON.parse(await fs.readFile(path.join(repoRoot, "data", fileName), "utf8"));
    visit(data);
  }

  for (const relativePath of referencedAssets) {
    await fs.access(path.join(outputDir, relativePath));
  }
}

async function collectSummary(directory) {
  let topLevel = 0;
  let files = 0;
  let bytes = 0;

  async function visit(current, isRoot) {
    const entries = await fs.readdir(current, { withFileTypes: true });
    if (isRoot) topLevel = entries.length;
    for (const entry of entries) {
      const fullPath = path.join(current, entry.name);
      if (entry.isDirectory()) {
        await visit(fullPath, false);
        continue;
      }
      if (entry.isFile()) {
        files += 1;
        bytes += (await fs.stat(fullPath)).size;
      }
    }
  }

  await visit(directory, true);
  return { topLevel, files, megabytes: Math.round((bytes / 1024 / 1024) * 10) / 10 };
}

assertSafeOutputPath();
await cleanOutputPreservingUserData();

run(process.execPath, ["tools/generate-maa-resource.mjs"]);
run(process.execPath, ["tools/generate-maa-interface.mjs"]);
run(process.execPath, ["tools/check-maa-contract.mjs"]);
run("dotnet", [
  "publish",
  "apps/rhodes-suki/RhodesSuki.csproj",
  "-c",
  "Release",
  "-r",
  "win-x64",
  "--self-contained",
  "true",
  "-p:PublishSingleFile=true",
  "-p:IncludeNativeLibrariesForSelfExtract=true",
  "-p:EnableCompressionInSingleFile=true",
  "-p:DebugType=None",
  "-p:DebugSymbols=false",
  "-o",
  "outputs/suki-portable",
]);

await removeFilesByExtension(outputDir, ".pdb");
await moveMaaAgentBinaryToLibs();
await copyMaaNativeRuntimeToRuntimes();
await copyRequiredMasterData();
await copyWebOverlayRuntime();
await copyWebOverlayAssets();

const exePath = path.join(outputDir, "RhodesSuki.exe");
await fs.access(exePath);
const summary = await collectSummary(outputDir);
console.log(
  `Suki portable published: ${path.relative(repoRoot, exePath)} ` +
    `(${summary.topLevel} top-level items, ${summary.files} files, ${summary.megabytes} MB)`,
);

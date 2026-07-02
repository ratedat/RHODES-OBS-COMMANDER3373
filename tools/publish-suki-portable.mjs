import { spawnSync } from "node:child_process";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const outputsRoot = path.join(repoRoot, "outputs");
const outputDir = path.join(outputsRoot, "suki-portable");

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
await fs.rm(outputDir, { recursive: true, force: true });
await fs.mkdir(outputDir, { recursive: true });

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

const exePath = path.join(outputDir, "RhodesSuki.exe");
await fs.access(exePath);
const summary = await collectSummary(outputDir);
console.log(
  `Suki portable published: ${path.relative(repoRoot, exePath)} ` +
    `(${summary.topLevel} top-level items, ${summary.files} files, ${summary.megabytes} MB)`,
);

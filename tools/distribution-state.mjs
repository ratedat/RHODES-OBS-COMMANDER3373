import fs from "node:fs/promises";
import path from "node:path";
import { randomUUID } from "node:crypto";

async function rejectRedirects(root, target) {
  for (let current = target; ; current = path.dirname(current)) {
    try {
      if ((await fs.lstat(current)).isSymbolicLink()) throw new Error("A redirected distribution path was refused.");
    } catch (error) { if (error.code !== "ENOENT") throw error; }
    if (path.relative(root, current) === "") break;
  }
}

export async function writeCleanDistributionState(repoRoot, outputRoot) {
  repoRoot = path.resolve(repoRoot);
  outputRoot = path.resolve(outputRoot);
  const relative = path.relative(repoRoot, outputRoot);
  if (!relative || relative === ".." || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
    throw new Error("Distribution state requires an output directory inside the repository, separate from the source root.");
  }
  const dataRoot = path.join(outputRoot, "data");
  const statePath = path.join(dataRoot, "current-state.json");
  const exampleTarget = path.join(dataRoot, "overlay-state.example.json");
  await rejectRedirects(repoRoot, statePath);
  await rejectRedirects(repoRoot, exampleTarget);
  const examplePath = path.join(repoRoot, "data", "overlay-state.example.json");
  await rejectRedirects(repoRoot, examplePath);
  const state = JSON.parse(await fs.readFile(examplePath, "utf8"));
  if (!state || typeof state !== "object" || Array.isArray(state)) throw new Error("Invalid distribution state example.");
  const serialized = `${JSON.stringify(state, null, 2)}\n`;
  await fs.mkdir(dataRoot, { recursive: true });
  for (const target of [statePath, exampleTarget]) {
    // Replacing the directory entry also preserves a live file if an old output was hard-linked to it.
    const temporary = `${target}.${randomUUID()}.tmp`;
    await fs.writeFile(temporary, serialized, { encoding: "utf8", flag: "wx" });
    try { await fs.rename(temporary, target); }
    finally { await fs.rm(temporary, { force: true }); }
  }
  return state;
}

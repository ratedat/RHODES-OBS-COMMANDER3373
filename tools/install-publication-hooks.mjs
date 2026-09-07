import { createHash } from "node:crypto";
import fs from "node:fs/promises";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const schemaVersion = 1;
const recordName = "publication-hooks.json";
const policyName = "publication-private.json";
const currentMarker = "# RHODES publication boundary installer v1";
const legacyMarker = "# RHODES publication boundary";
const hookModes = new Map([
  ["pre-commit", "--staged"],
  ["pre-push", "--push"],
]);

function digest(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

function guardDigest(bytes) {
  return digest(Buffer.from(bytes.toString("utf8").replaceAll("\r\n", "\n")));
}

function git(root, args, { optional = false } = {}) {
  const result = spawnSync("git", ["-C", root, ...args], { encoding: "utf8" });
  if (result.error) throw result.error;
  if (result.status !== 0 && !optional) throw new Error(`Publication hook setup: git ${args[0]} failed.`);
  return result;
}

function shellQuote(value) {
  if (/[\0\r\n]/.test(value)) throw new Error("The Node target cannot be represented safely in a hook.");
  return `'${value.replaceAll("'", `'\"'\"'`)}'`;
}

function portableNodePath(value) {
  if (!path.isAbsolute(value)) throw new Error("The Node target must be an absolute path.");
  return value.replaceAll("\\", "/");
}

function hookContent(nodePath, mode) {
  return [
    "#!/bin/sh",
    currentMarker,
    "root=$(git rev-parse --show-toplevel) || exit 1",
    `exec ${shellQuote(nodePath)} "$root/tools/publication-boundary.mjs" ${mode} "$root"`,
    "",
  ].join("\n");
}

async function repositoryLayout(repoRoot) {
  repoRoot = await fs.realpath(path.resolve(repoRoot));
  const top = git(repoRoot, ["rev-parse", "--show-toplevel"], { optional: true });
  const common = git(repoRoot, ["rev-parse", "--git-common-dir"], { optional: true });
  if (top.status !== 0 || common.status !== 0) throw new Error("Publication hook setup requires a Git working tree.");
  const hooksSetting = git(repoRoot, ["config", "--get", "core.hooksPath"], { optional: true });
  if (hooksSetting.status === 0) {
    throw new Error("A configured core.hooksPath is outside the managed publication hook layout.");
  }
  if (hooksSetting.status !== 0 && hooksSetting.status !== 1) {
    throw new Error("Unable to determine core.hooksPath safely.");
  }
  const topLevel = await fs.realpath(path.resolve(repoRoot, top.stdout.trim()));
  const commonDir = path.resolve(repoRoot, common.stdout.trim());
  return {
    repoRoot: topLevel,
    commonDir,
    hooksDir: path.join(commonDir, "hooks"),
    infoDir: path.join(commonDir, "info"),
  };
}

async function readRequired(repoRoot) {
  const result = git(repoRoot, ["config", "--local", "--bool", "--get", "publication.policyRequired"], { optional: true });
  if (result.status === 1) return false;
  if (result.status !== 0 || !["true", "false"].includes(result.stdout.trim())) {
    throw new Error("The publication.policyRequired setting is invalid.");
  }
  return result.stdout.trim() === "true";
}

async function readPolicy(policyPath, required) {
  let bytes;
  try {
    bytes = await fs.readFile(policyPath);
  } catch (error) {
    if (error.code === "ENOENT" && !required) return null;
    if (error.code === "ENOENT") throw new Error("The required local publication policy is missing or unreadable.");
    throw new Error("The local publication policy is unreadable.");
  }
  let policy;
  try {
    policy = JSON.parse(bytes.toString("utf8"));
  } catch {
    throw new Error("The local publication policy is unreadable.");
  }
  if (policy.schemaVersion !== schemaVersion) throw new Error("Unsupported local publication policy.");
  return bytes;
}

async function writeIfChanged(file, bytes, mode) {
  try {
    if ((await fs.readFile(file)).equals(bytes)) {
      if (mode !== undefined) await fs.chmod(file, mode);
      return;
    }
  } catch (error) {
    if (error.code !== "ENOENT") throw error;
  }
  await fs.writeFile(file, bytes, mode === undefined ? undefined : { mode });
  if (mode !== undefined) await fs.chmod(file, mode);
}

function makeRecord(nodePath, guardBytes) {
  const hooks = {};
  for (const [name, mode] of hookModes) {
    const bytes = Buffer.from(hookContent(nodePath, mode));
    hooks[name] = { mode, sha256: digest(bytes) };
  }
  return {
    schemaVersion,
    node: { path: nodePath },
    guard: { relativePath: "tools/publication-boundary.mjs", normalization: "lf", sha256: guardDigest(guardBytes) },
    hooks,
  };
}

async function readGuard(repoRoot) {
  const guardPath = path.join(repoRoot, "tools", "publication-boundary.mjs");
  let bytes;
  try {
    bytes = await fs.readFile(guardPath);
  } catch {
    throw new Error("The publication guard is missing or unreadable.");
  }
  return { guardPath, bytes };
}

async function assertNodeTarget(nodePath) {
  let nodeInfo;
  try {
    nodeInfo = await fs.stat(nodePath);
  } catch {
    throw new Error("The publication hook Node target is missing or unreadable.");
  }
  if (!nodeInfo.isFile()) throw new Error("The publication hook Node target is not a file.");
}

function hasMarkerLine(value, marker) {
  return value.split(/\r?\n/).includes(marker);
}

function isManagedHook(value) {
  return hasMarkerLine(value, currentMarker);
}

function isLegacyHook(value) {
  return hasMarkerLine(value, legacyMarker) && !isManagedHook(value);
}

export async function installPublicationHooks(repoRoot, options = {}) {
  const layout = await repositoryLayout(repoRoot);
  const nodePath = portableNodePath(options.nodePath ?? process.execPath);
  await assertNodeTarget(nodePath);
  const { bytes: guardBytes } = await readGuard(layout.repoRoot);
  const required = await readRequired(layout.repoRoot);
  const policyPath = path.join(layout.infoDir, policyName);
  const policyBytes = await readPolicy(policyPath, required);

  const existingHooks = new Map();
  for (const name of hookModes.keys()) {
    const hookPath = path.join(layout.hooksDir, name);
    try {
      const value = await fs.readFile(hookPath, "utf8");
      if (!isManagedHook(value) && !isLegacyHook(value)) throw new Error(`Refusing to replace an unknown ${name} hook.`);
      existingHooks.set(name, { hookPath, value, legacy: isLegacyHook(value) });
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
      existingHooks.set(name, { hookPath, value: null, legacy: false });
    }
  }

  await fs.mkdir(layout.infoDir, { recursive: true });
  await fs.mkdir(layout.hooksDir, { recursive: true });
  for (const [name, existing] of existingHooks) {
    if (!existing.legacy) continue;
    const backupPath = `${existing.hookPath}.publication-boundary-backup`;
    try {
      const backup = await fs.readFile(backupPath, "utf8");
      if (backup !== existing.value) throw new Error(`A different ${name} hook backup already exists.`);
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
      await fs.writeFile(backupPath, existing.value, { flag: "wx" });
    }
  }

  if (policyBytes === null) {
    await fs.writeFile(policyPath, `${JSON.stringify({ schemaVersion }, null, 2)}\n`, { flag: "wx" });
  }
  git(layout.repoRoot, ["config", "--local", "publication.policyRequired", "true"]);

  const record = makeRecord(nodePath, guardBytes);
  for (const [name, mode] of hookModes) {
    await writeIfChanged(path.join(layout.hooksDir, name), Buffer.from(hookContent(nodePath, mode)), 0o755);
  }
  const recordBytes = Buffer.from(`${JSON.stringify(record, null, 2)}\n`);
  await writeIfChanged(path.join(layout.infoDir, recordName), recordBytes);
  return checkPublicationHooks(layout.repoRoot, { nodePath });
}

function validSha256(value) {
  return typeof value === "string" && /^[a-f0-9]{64}$/.test(value);
}

export async function checkPublicationHooks(repoRoot, options = {}) {
  const layout = await repositoryLayout(repoRoot);
  const required = await readRequired(layout.repoRoot);
  if (!required) throw new Error("The required local publication policy setting is disabled.");
  await readPolicy(path.join(layout.infoDir, policyName), true);

  let record;
  try {
    record = JSON.parse(await fs.readFile(path.join(layout.infoDir, recordName), "utf8"));
  } catch {
    throw new Error("The publication hook verification record is missing or unreadable.");
  }
  if (record?.schemaVersion !== schemaVersion || record.guard?.relativePath !== "tools/publication-boundary.mjs" ||
      record.guard?.normalization !== "lf" ||
      !validSha256(record.guard?.sha256) || typeof record.node?.path !== "string") {
    throw new Error("The publication hook verification record is invalid.");
  }

  const expectedNode = portableNodePath(options.nodePath ?? process.execPath);
  if (record.node.path !== expectedNode) throw new Error("The publication hook Node target does not match this runtime.");
  await assertNodeTarget(record.node.path);

  const { bytes: guardBytes } = await readGuard(layout.repoRoot);
  if (guardDigest(guardBytes) !== record.guard.sha256) throw new Error("The publication guard SHA-256 does not match the installed record.");

  for (const [name, mode] of hookModes) {
    const hookRecord = record.hooks?.[name];
    if (hookRecord?.mode !== mode || !validSha256(hookRecord?.sha256)) {
      throw new Error(`The ${name} hook verification record is invalid.`);
    }
    let bytes;
    try {
      bytes = await fs.readFile(path.join(layout.hooksDir, name));
    } catch {
      throw new Error(`The ${name} hook is missing or unreadable.`);
    }
    const expected = Buffer.from(hookContent(record.node.path, mode));
    if (digest(bytes) !== hookRecord.sha256 || !bytes.equals(expected)) {
      throw new Error(`The ${name} hook content does not match the installed record.`);
    }
  }
  return { ...layout, record };
}

async function main(arguments_) {
  const args = [...arguments_];
  const mode = args.shift();
  let rootArgument;
  if (args[0] === "--root" && args.length === 2) {
    [, rootArgument] = args;
  } else if (args.length !== 0) {
    throw new Error("Usage: node tools/install-publication-hooks.mjs (--install|--check) [--root repository]");
  }
  if (!["--install", "--check"].includes(mode)) {
    throw new Error("Usage: node tools/install-publication-hooks.mjs (--install|--check) [--root repository]");
  }
  const repoRoot = rootArgument ? path.resolve(rootArgument) : process.cwd();
  if (mode === "--install") {
    await installPublicationHooks(repoRoot);
    console.log("Publication hooks installed and verified metadata recorded.");
  } else {
    await checkPublicationHooks(repoRoot);
    console.log("Publication hooks and local policy verified.");
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    await main(process.argv.slice(2));
  } catch (error) {
    console.error(error instanceof Error ? error.message : "Publication hook operation failed.");
    process.exitCode = 1;
  }
}

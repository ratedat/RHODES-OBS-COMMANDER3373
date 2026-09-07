import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import {
  checkPublicationHooks,
  installPublicationHooks,
} from "../tools/install-publication-hooks.mjs";

const guardSource = new URL("../tools/publication-boundary.mjs", import.meta.url);
const installerPath = fileURLToPath(new URL("../tools/install-publication-hooks.mjs", import.meta.url));

function git(root, args, fail = false) {
  const result = spawnSync("git", ["-C", root, ...args], { encoding: "utf8" });
  if (!fail) assert.equal(result.status, 0, result.stderr);
  return result;
}

async function write(root, name, value) {
  const target = path.join(root, name);
  await fs.mkdir(path.dirname(target), { recursive: true });
  await fs.writeFile(target, value);
}

async function fixture(t) {
  const base = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes hooks test "));
  const root = path.join(base, "public repo");
  await fs.mkdir(root);
  t.after(() => fs.rm(base, { recursive: true, force: true }));
  git(root, ["init", "-b", "main"]);
  git(root, ["config", "user.name", "Publication hooks test"]);
  git(root, ["config", "user.email", "test@example.invalid"]);
  await write(root, "tools/publication-boundary.mjs", await fs.readFile(guardSource));
  await write(root, "public.txt", "Clean public baseline\n");
  git(root, ["add", "tools/publication-boundary.mjs", "public.txt"]);
  git(root, ["commit", "-m", "Synthetic baseline"]);
  return { base, root };
}

async function commonDir(root) {
  return path.resolve(root, git(root, ["rev-parse", "--git-common-dir"]).stdout.trim());
}

test("installs portable shared hooks and a generic required policy idempotently", async t => {
  const { base, root } = await fixture(t);
  const installed = spawnSync(process.execPath, [installerPath, "--install", "--root", root], { encoding: "utf8" });
  assert.equal(installed.status, 0, installed.stderr);
  assert.match(installed.stdout, /installed/i);
  await checkPublicationHooks(root);

  const common = await commonDir(root);
  const policyPath = path.join(common, "info", "publication-private.json");
  const recordPath = path.join(common, "info", "publication-hooks.json");
  const hookPaths = [path.join(common, "hooks", "pre-commit"), path.join(common, "hooks", "pre-push")];
  assert.deepEqual(JSON.parse(await fs.readFile(policyPath, "utf8")), { schemaVersion: 1 });
  assert.equal(git(root, ["config", "--local", "--get", "publication.policyRequired"]).stdout.trim(), "true");
  for (const hookPath of hookPaths) {
    const hook = await fs.readFile(hookPath, "utf8");
    assert.match(hook, /git rev-parse --show-toplevel/);
    assert.match(hook, /\$root\/tools\/publication-boundary\.mjs/);
    assert.doesNotMatch(hook, /public repo/i);
  }

  const before = await Promise.all([recordPath, policyPath, ...hookPaths].map(file => fs.readFile(file)));
  await installPublicationHooks(root);
  const after = await Promise.all([recordPath, policyPath, ...hookPaths].map(file => fs.readFile(file)));
  assert.deepEqual(after, before);
  const checked = spawnSync(process.execPath, [installerPath, "--check", "--root", root], { encoding: "utf8" });
  assert.equal(checked.status, 0, checked.stderr);
  assert.match(checked.stdout, /verified/i);

  const linked = path.join(base, "linked worktree");
  git(root, ["worktree", "add", "-b", "linked-test", linked]);
  await checkPublicationHooks(linked);
  await write(linked, "worktree.txt", "Clean linked worktree content\n");
  git(linked, ["add", "worktree.txt"]);
  git(linked, ["commit", "-m", "Clean linked worktree commit"]);

  const remote = path.join(base, "remote repo.git");
  git(base, ["init", "--bare", remote]);
  git(linked, ["remote", "add", "origin", remote]);
  git(linked, ["push", "origin", "linked-test"]);

  const quotedNode = path.join(root, "node's target.exe");
  await fs.writeFile(quotedNode, "synthetic executable placeholder");
  await installPublicationHooks(root, { nodePath: quotedNode });
  const quotedHook = await fs.readFile(path.join(common, "hooks", "pre-commit"), "utf8");
  const portableQuotedNode = quotedNode.replaceAll("\\", "/").replaceAll("'", `'\"'\"'`);
  assert.ok(quotedHook.includes(`'${portableQuotedNode}'`));
});

test("check fails closed for hook, guard, record node, and required-policy tampering", async t => {
  const { root } = await fixture(t);
  await installPublicationHooks(root);
  const common = await commonDir(root);
  const hookPath = path.join(common, "hooks", "pre-commit");
  const guardPath = path.join(root, "tools", "publication-boundary.mjs");
  const recordPath = path.join(common, "info", "publication-hooks.json");
  const policyPath = path.join(common, "info", "publication-private.json");

  await fs.appendFile(hookPath, "# tampered\n");
  const cliCheck = spawnSync(process.execPath, [installerPath, "--check", "--root", root], { encoding: "utf8" });
  assert.notEqual(cliCheck.status, 0);
  assert.match(cliCheck.stderr, /pre-commit hook.*does not match/i);
  assert.doesNotMatch(cliCheck.stderr, /\n\s+at /);
  await assert.rejects(checkPublicationHooks(root), /pre-commit hook.*does not match/i);
  await installPublicationHooks(root);
  await fs.appendFile(guardPath, "\n// tampered\n");
  await assert.rejects(checkPublicationHooks(root), /guard.*SHA-256/i);
  await fs.writeFile(guardPath, await fs.readFile(guardSource));

  const record = JSON.parse(await fs.readFile(recordPath, "utf8"));
  record.node.path = path.join(path.dirname(process.execPath), "missing-node-target.exe");
  await fs.writeFile(recordPath, `${JSON.stringify(record, null, 2)}\n`);
  await assert.rejects(checkPublicationHooks(root), /Node target/i);

  await installPublicationHooks(root);
  await fs.rm(policyPath);
  await assert.rejects(checkPublicationHooks(root), /required local publication policy/i);
  await assert.rejects(installPublicationHooks(root), /required local publication policy/i);
});

test("preserves policy bytes, migrates known legacy hooks, and refuses unknown hook routing", async t => {
  const { root } = await fixture(t);
  const common = await commonDir(root);
  const policyPath = path.join(common, "info", "publication-private.json");
  const policyBytes = Buffer.from('{\n  "schemaVersion": 1,\n  "blockedText": ["synthetic-local-only-marker"]\n}\n');
  await fs.writeFile(policyPath, policyBytes);
  const legacyPath = path.join(common, "hooks", "pre-commit");
  const legacy = "#!/bin/sh\n# RHODES publication boundary\nexit 0\n";
  await fs.writeFile(legacyPath, legacy);
  await installPublicationHooks(root);
  assert.deepEqual(await fs.readFile(policyPath), policyBytes);
  assert.equal(await fs.readFile(`${legacyPath}.publication-boundary-backup`, "utf8"), legacy);
  await checkPublicationHooks(root);

  const second = await fixture(t);
  const secondCommon = await commonDir(second.root);
  const unknown = "#!/bin/sh\necho unrelated hook\n";
  await fs.writeFile(path.join(secondCommon, "hooks", "pre-commit"), unknown);
  await assert.rejects(installPublicationHooks(second.root), /unknown pre-commit hook/i);
  assert.equal(await fs.readFile(path.join(secondCommon, "hooks", "pre-commit"), "utf8"), unknown);

  const third = await fixture(t);
  git(third.root, ["config", "core.hooksPath", ".custom-hooks"]);
  await assert.rejects(installPublicationHooks(third.root), /core\.hooksPath/i);
  await assert.rejects(checkPublicationHooks(third.root), /core\.hooksPath/i);
});

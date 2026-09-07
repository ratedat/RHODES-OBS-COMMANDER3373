import test from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { createPublicationGuard, sourceFingerprints } from "../tools/publication-boundary.mjs";
import { installPublicationHooks } from "../tools/install-publication-hooks.mjs";

const zero = "0".repeat(40);
const syntheticSource = Array.from({ length: 9 }, (_, i) =>
  `const syntheticPrivateValue${i} = decodeFixtureRecord(record.sample${i}, "artificial-secret-fixture-${i}");`).join("\n");
function git(root, args, fail = false) {
  const result = spawnSync("git", ["-C", root, ...args], { encoding: "utf8" });
  if (!fail) assert.equal(result.status, 0, result.stderr);
  return result;
}
async function fixture(t) {
  const base = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-publication-test-"));
  const root = path.join(base, "public");
  const privateRoot = path.join(base, "private");
  await fs.mkdir(root); await fs.mkdir(privateRoot);
  t.after(() => fs.rm(base, { recursive: true, force: true }));
  git(root, ["init", "-b", "main"]);
  git(root, ["config", "user.name", "Publication test"]);
  git(root, ["config", "user.email", "test@example.invalid"]);
  await fs.writeFile(path.join(privateRoot, "source.ts"), syntheticSource);
  const policy = { schemaVersion: 1, privateRoots: [privateRoot],
    fingerprintRoots: [{ path: privateRoot, extensions: [".ts"] }], blockedText: ["synthetic-private-marker"] };
  const guard = await createPublicationGuard(root, { policy });
  return { base, root, privateRoot, policy, guard };
}
async function write(root, name, value) {
  await fs.mkdir(path.dirname(path.join(root, name)), { recursive: true });
  await fs.writeFile(path.join(root, name), value);
}
function commit(root, message = "Synthetic public commit") {
  git(root, ["add", "-A"]); git(root, ["commit", "-m", message]);
  return git(root, ["rev-parse", "HEAD"]).stdout.trim();
}

test("copies public files and omits ignored logs, while refusing private files", async t => {
  const { root, guard } = await fixture(t);
  await write(root, "input/ui.js", "export const publicMessage = '公開画面';\n");
  await write(root, "input/server.log", "synthetic-private-marker");
  await guard.copyTree(path.join(root, "input"), path.join(root, "out"));
  assert.equal(await fs.readFile(path.join(root, "out/ui.js"), "utf8"), "export const publicMessage = '公開画面';\n");
  await assert.rejects(fs.access(path.join(root, "out/server.log")), { code: "ENOENT" });
  await guard.checkTree(path.join(root, "out"));
  await write(root, "input/renamed.dat", syntheticSource);
  await assert.rejects(guard.copyTree(path.join(root, "input"), path.join(root, "bad-out")), /fingerprint refused/);
  await assert.rejects(fs.access(path.join(root, "bad-out")), { code: "ENOENT" });
});

test("matches source fragments after renaming, whitespace changes and UTF-16 encoding", async t => {
  const { root, guard } = await fixture(t);
  const fragment = syntheticSource.split("\n").slice(2, 6).map(line => `    ${line.replaceAll(" = ", "  =  ")}`).join("\n");
  assert.ok(sourceFingerprints(Buffer.from(fragment)).length > 0);
  await write(root, "out/notes.txt", `A synthetic preface\n${fragment}\nA synthetic suffix`);
  await assert.rejects(guard.checkTree(path.join(root, "out")), /source excerpt refused/);
  await write(root, "out/notes.txt", Buffer.from(`\ufeff${fragment}`, "utf16le"));
  await assert.rejects(guard.checkTree(path.join(root, "out")), /source excerpt refused/);
});

test("uppercase policy hashes are normalized and malformed hashes fail closed", async t => {
  const { root, policy } = await fixture(t);
  const bytes = Buffer.from("An entirely synthetic private binary fixture");
  await write(root, "out/renamed.bin", bytes);
  const hash = createHash("sha256").update(bytes).digest("hex").toUpperCase();
  const guard = await createPublicationGuard(root, { policy: { ...policy, blockedSha256: [hash] } });
  await assert.rejects(guard.checkTree(path.join(root, "out")), /fingerprint refused/);
  await assert.rejects(createPublicationGuard(root, { policy: { ...policy, blockedSha256: ["invalid"] } }), /Invalid publication fingerprint/);
});

test("refuses redirected input and destination directories", async t => {
  const { root, privateRoot, guard } = await fixture(t);
  await fs.symlink(privateRoot, path.join(root, "linked"), process.platform === "win32" ? "junction" : "dir");
  await assert.rejects(guard.checkTree(path.join(root, "linked")), /Redirected/);
  await write(root, "safe/message.txt", "Public fixture");
  await assert.rejects(guard.copyTree(path.join(root, "safe"), path.join(root, "linked/out")), /Linked publication destination/);
  await assert.rejects(guard.assertDestination(path.join(root, "linked/out")), /Linked publication destination/);
  await assert.rejects(guard.checkTree(privateRoot), /outside the repository/);
});

test("required local policy cannot silently disappear", async t => {
  const { root, policy } = await fixture(t);
  git(root, ["config", "publication.policyRequired", "true"]);
  await assert.rejects(createPublicationGuard(root), /required local publication policy/);
  await write(root, ".git/info/publication-private.json", JSON.stringify(policy));
  await createPublicationGuard(root);
  await write(root, ".git/info/publication-private.json", "broken JSON");
  await assert.rejects(createPublicationGuard(root), /required local publication policy/);
});

test("forced ignored files and staged bytes are checked instead of their worktree replacement", async t => {
  const { root, guard } = await fixture(t);
  await write(root, ".gitignore", "hidden.txt\n");
  await write(root, "hidden.txt", syntheticSource);
  git(root, ["add", "-f", "hidden.txt"]);
  await write(root, "hidden.txt", "This worktree replacement is public.");
  assert.throws(() => guard.checkStaged(), /fingerprint refused/);
});

test("checks multiple Git batches with odd-sized multilingual blobs", async t => {
  const { root, guard } = await fixture(t);
  for (let index = 0; index < 75; index += 1) {
    const value = `公開用の人工データ ${index}\n${"plain text sample ".repeat(220)}`;
    const bytes = Buffer.from(value);
    await write(root, `records/記録-${index}.txt`, bytes.length % 2 ? bytes : Buffer.concat([bytes, Buffer.from("x")]));
  }
  git(root, ["add", "records"]);
  guard.checkStaged();
});

test("outgoing history is checked even if its private file was later removed", async t => {
  const { root, guard } = await fixture(t);
  await write(root, "public.txt", "Public baseline");
  const base = commit(root);
  await write(root, "ordinary.txt", syntheticSource); commit(root);
  await fs.rm(path.join(root, "ordinary.txt"));
  const head = commit(root);
  assert.throws(() => guard.checkPush(`refs/heads/main ${head} refs/heads/main ${base}\n`), /fingerprint refused/);
});

test("internal refs, tree links, reference names and commit metadata are checked", async t => {
  const { root, guard } = await fixture(t);
  await write(root, "public.txt", "Public baseline");
  const base = commit(root);
  guard.checkPush(`refs/heads/main ${base} refs/heads/main ${zero}\n`);
  assert.throws(() => guard.checkPush(`refs/codex/snapshot ${base} refs/codex/snapshot ${zero}\n`), /Internal Git references/);
  assert.throws(() => guard.checkPush(`refs/heads/private-debug ${base} refs/heads/main ${zero}\n`), /Non-public path refused/);
  assert.throws(() => guard.checkPush(`refs/heads/synthetic-private-marker ${base} refs/heads/main ${zero}\n`), /Private content refused/);
  git(root, ["update-index", "--add", "--cacheinfo", `160000,${base},vendor/module`]);
  await assert.rejects(guard.checkGitWorktree(), /Linked Git entry/);
  git(root, ["commit", "-m", "Synthetic gitlink fixture"]);
  const linked = git(root, ["rev-parse", "HEAD"]).stdout.trim();
  assert.throws(() => guard.checkPush(`refs/heads/main ${linked} refs/heads/main ${base}\n`), /Linked Git object refused/);
  git(root, ["update-index", "--force-remove", "vendor/module"]);
  git(root, ["commit", "-m", "synthetic-private-marker"]);
  const metadata = git(root, ["rev-parse", "HEAD"]).stdout.trim();
  assert.throws(() => guard.checkPush(`refs/heads/main ${metadata} refs/heads/main ${linked}\n`), /Private content refused/);
});

test("renamed archives and dumps are refused; only an exact reviewed archive is permitted", async t => {
  const { root, policy, guard } = await fixture(t);
  const bytes = Buffer.concat([Buffer.from([0x50, 0x4b, 0x03, 0x04]), Buffer.from("synthetic zip fixture")]);
  await write(root, "out/renamed.bin", bytes);
  await assert.rejects(guard.checkTree(path.join(root, "out")), /Nested archive refused/);
  const approved = await createPublicationGuard(root, { policy: { ...policy,
    allowedArchiveSha256: [createHash("sha256").update(bytes).digest("hex")] } });
  await approved.checkTree(path.join(root, "out"));
  await write(root, "out/renamed.bin", Buffer.from("PAGEDU64 synthetic memory fixture"));
  await assert.rejects(guard.checkTree(path.join(root, "out")), /Memory dump refused/);
});

test("refuses high-confidence credential material without echoing its value", async t => {
  const { root, guard } = await fixture(t);
  const samples = [
    ["pem.txt", ["-----BEGIN ", "PRIVATE KEY-----", "\nsynthetic fixture\n", "-----END PRIVATE KEY-----"].join("")],
    ["github.txt", ["gh", "p_", "A".repeat(36)].join("")],
    ["openai.txt", ["sk-", "B".repeat(48)].join("")],
  ];
  for (const [name, secret] of samples) {
    await write(root, `out/${name}`, secret);
    await assert.rejects(guard.checkTree(path.join(root, "out")), error => {
      assert.match(error.message, /Credential material refused/);
      assert.doesNotMatch(error.message, new RegExp(secret.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")));
      return true;
    });
    await fs.rm(path.join(root, "out", name));
  }
});

test("actual commit and push hooks block local synthetic publication and allow clean content", async t => {
  const { base, root, policy } = await fixture(t);
  await write(root, "tools/publication-boundary.mjs", await fs.readFile(new URL("../tools/publication-boundary.mjs", import.meta.url)));
  await write(root, ".git/info/publication-private.json", JSON.stringify(policy));
  await installPublicationHooks(root);
  await write(root, "public.txt", "Clean public content");
  commit(root);
  const remote = path.join(base, "remote.git");
  git(base, ["init", "--bare", remote]);
  git(root, ["remote", "add", "origin", remote]);
  git(root, ["push", "origin", "main"]);
  const originalRemote = git(remote, ["rev-parse", "refs/heads/main"]).stdout.trim();
  await write(root, "ordinary.txt", syntheticSource);
  git(root, ["add", "ordinary.txt"]);
  assert.notEqual(git(root, ["commit", "-m", "Must be blocked"], true).status, 0);
  git(root, ["-c", "core.hooksPath=", "commit", "-m", "Create synthetic pre-existing leak for push test"]);
  const rejected = git(root, ["push", "origin", "main"], true);
  assert.notEqual(rejected.status, 0);
  assert.match(rejected.stderr, /fingerprint refused/);
  assert.equal(git(remote, ["rev-parse", "refs/heads/main"]).stdout.trim(), originalRemote);
  git(root, ["update-ref", "refs/codex/snapshot", originalRemote]);
  const mirror = git(root, ["push", "--mirror", "origin"], true);
  assert.notEqual(mirror.status, 0);
  assert.equal(git(remote, ["show-ref", "--verify", "refs/codex/snapshot"], true).status, 128);
});

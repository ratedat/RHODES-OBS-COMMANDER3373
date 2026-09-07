import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { writeCleanDistributionState } from "../tools/distribution-state.mjs";

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "rhodes-distribution-state-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  await fs.mkdir(path.join(root, "data"));
  const example = { version: 1, mode: "casual", run: { campaignId: "is2_phantom" }, relics: [], operators: [], adb: { serial: "" } };
  await fs.writeFile(path.join(root, "data", "overlay-state.example.json"), JSON.stringify(example));
  const live = JSON.stringify({ relics: ["artificial-owned-item"], adb: { serial: "artificial-device" } });
  await fs.writeFile(path.join(root, "data", "current-state.json"), live);
  return { root, example, live };
}

test("distribution state comes from the tracked example and leaves live state intact", async t => {
  const { root, example, live } = await fixture(t);
  const output = path.join(root, "outputs", "package with spaces");
  await fs.mkdir(path.join(output, "data"), { recursive: true });
  await fs.writeFile(path.join(output, "data", "current-state.json"), live);
  assert.deepEqual(await writeCleanDistributionState(root, output), example);
  assert.deepEqual(JSON.parse(await fs.readFile(path.join(output, "data", "current-state.json"), "utf8")), example);
  assert.deepEqual(JSON.parse(await fs.readFile(path.join(output, "data", "overlay-state.example.json"), "utf8")), example);
  assert.equal(await fs.readFile(path.join(root, "data", "current-state.json"), "utf8"), live);
});

test("a clean checkout needs no live current-state file to prepare a distribution", async t => {
  const { root, example } = await fixture(t);
  await fs.unlink(path.join(root, "data", "current-state.json"));
  assert.deepEqual(await writeCleanDistributionState(root, path.join(root, "outputs", "fresh")), example);
});

test("replacing an old hard-linked distribution state preserves the live file", async t => {
  const { root, example, live } = await fixture(t);
  const output = path.join(root, "outputs", "hard-linked");
  await fs.mkdir(path.join(output, "data"), { recursive: true });
  await fs.link(path.join(root, "data", "current-state.json"), path.join(output, "data", "current-state.json"));
  await writeCleanDistributionState(root, output);
  assert.equal(await fs.readFile(path.join(root, "data", "current-state.json"), "utf8"), live);
  assert.deepEqual(JSON.parse(await fs.readFile(path.join(output, "data", "current-state.json"), "utf8")), example);
});

test("distribution preparation refuses the source root and paths outside it", async t => {
  const { root, live } = await fixture(t);
  await assert.rejects(writeCleanDistributionState(root, root), /output directory/);
  await assert.rejects(writeCleanDistributionState(root, path.join(root, "..", "elsewhere")), /output directory/);
  assert.equal(await fs.readFile(path.join(root, "data", "current-state.json"), "utf8"), live);
});

test("a redirected output cannot overwrite the live source data", async t => {
  const { root, live } = await fixture(t);
  const output = path.join(root, "outputs", "redirected");
  await fs.mkdir(output, { recursive: true });
  await fs.symlink(path.join(root, "data"), path.join(output, "data"), process.platform === "win32" ? "junction" : "dir");
  await assert.rejects(writeCleanDistributionState(root, output), /redirected/);
  assert.equal(await fs.readFile(path.join(root, "data", "current-state.json"), "utf8"), live);
});

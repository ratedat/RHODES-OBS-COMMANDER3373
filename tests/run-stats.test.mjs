import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { updateRunField } from "../app/control-actions.js";
import {
  ABANDONED_RUN_STAT_FIELD_IDS,
  formatRunStatValue,
  isAbandonedRunStatField,
  normalizeRunStatValue,
  normalizeRunStats,
  runStatDisplayItems,
} from "../app/domain/run-stats.js";

test("run stat abandoned field policy stays synchronized with MAA target manifest", async () => {
  const manifest = JSON.parse(await readFile(new URL("../data/recognition/maa-recognition-target-policy.json", import.meta.url), "utf8"));
  const abandonedFields = manifest.runRecognition.abandonedFields;

  assert.deepEqual(ABANDONED_RUN_STAT_FIELD_IDS, abandonedFields);
  for (const field of abandonedFields) {
    assert.equal(isAbandonedRunStatField(field), true, `${field} should be abandoned in runtime filtering`);
  }
  assert.equal(isAbandonedRunStatField("ingot"), false);
});

test("normalizeRunStatValue accepts blank values as unset", () => {
  assert.equal(normalizeRunStatValue("hope", ""), null);
  assert.equal(formatRunStatValue({}, "ingot"), "-");
});

test("normalizeRunStatValue clamps retained numeric run resources", () => {
  assert.equal(normalizeRunStatValue("hope", "12.9"), null);
  assert.equal(normalizeRunStatValue("maxHope", "11"), null);
  assert.equal(normalizeRunStatValue("lifePoints", "-4"), null);
  assert.equal(normalizeRunStatValue("shield", "1200"), null);
  assert.equal(normalizeRunStatValue("ingot", "20"), 20);
  assert.equal(normalizeRunStatValue("commandLevel", "120"), null);
});

test("normalizeRunStats keeps only retained run stat fields", () => {
  const run = normalizeRunStats({ hope: "8", maxHope: "11", ingot: "20", lifePoints: undefined, shield: "bad", commandLevel: "4" });
  assert.deepEqual(run, { ingot: 20 });
});

test("updateRunField writes only retained numeric run stat fields", () => {
  const state = { run: {}, preferences: {} };
  updateRunField(state, "hope", "18");
  updateRunField(state, "maxHope", "11");
  updateRunField(state, "lifePoints", "5");
  updateRunField(state, "ingot", "20");
  updateRunField(state, "shield", "");
  updateRunField(state, "commandLevel", "3");
  assert.equal(state.run.hope, undefined);
  assert.equal(state.run.maxHope, undefined);
  assert.equal(state.run.lifePoints, undefined);
  assert.equal(state.run.ingot, 20);
  assert.equal(state.run.shield, undefined);
  assert.equal(state.run.commandLevel, undefined);
  assert.deepEqual(runStatDisplayItems(state.run).map((item) => [item.id, item.value]), [
    ["ingot", "20"],
  ]);
});

test("overlay state example omits abandoned run stat fields", async () => {
  const example = JSON.parse(await readFile(new URL("../data/overlay-state.example.json", import.meta.url), "utf8"));
  for (const field of ["hope", "maxHope", "lifePoints", "shield", "commandLevel"]) {
    assert.equal(Object.hasOwn(example.run, field), false, `${field} should not be part of the base state example`);
  }
  assert.equal(Object.hasOwn(example.run, "ingot"), true);
});

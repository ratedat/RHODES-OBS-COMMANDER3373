import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

import {
  applyTournamentRemoteOperation,
  buildTournamentRemoteSnapshot,
} from "../app/domain/tournament-remote-operations.js";

async function loadMaster() {
  const [campaigns, squads, relics, operators, effects, tiers, performances] = await Promise.all([
    readFile(new URL("../data/campaigns.json", import.meta.url), "utf8").then(JSON.parse),
    readFile(new URL("../data/squads.json", import.meta.url), "utf8").then(JSON.parse),
    readFile(new URL("../data/relics.json", import.meta.url), "utf8").then(JSON.parse),
    readFile(new URL("../data/operators.json", import.meta.url), "utf8").then(JSON.parse),
    readFile(new URL("../data/selectable-effects.json", import.meta.url), "utf8").then(JSON.parse),
    readFile(new URL("../data/difficulty-tiers.json", import.meta.url), "utf8").then(JSON.parse),
    readFile(new URL("../data/performances.json", import.meta.url), "utf8").then(JSON.parse),
  ]);
  return {
    campaigns,
    squads: squads.squads,
    relics: relics.relics,
    operators: operators.operators,
    selectableEffects: effects.selectableEffects,
    difficultyTiers: tiers.campaignDifficultyTiers,
    performances: performances.performances,
  };
}

function baseState() {
  return {
    version: 1,
    run: {
      campaignId: "is3_mizuki",
      ingot: 7,
      difficulty: 4,
      squadId: null,
      special: { is3_mizuki: {} },
    },
    operators: [],
    operatorCounts: {},
    relics: [],
    usedRelicIds: [],
    bossFlags: [],
    bossSelections: {},
    adb: { adbPath: "M:/private/adb.exe", serial: "127.0.0.1:16384" },
    preferences: { overlay: { backgroundEnabled: false } },
  };
}

test("remote operations update only allowed state fields", async () => {
  const master = await loadMaster();
  const state = baseState();
  const operator = master.operators.find((item) => item.id.startsWith("reserve_"));
  const relic = master.relics.find((item) => item.id);

  const runResult = applyTournamentRemoteOperation(state, master, {
    type: "run.set",
    field: "ingot",
    value: 31,
  });
  const operatorResult = applyTournamentRemoteOperation(runResult.state, master, {
    type: "operator.set",
    operatorId: operator.id,
    selected: true,
    count: 2,
  });
  const relicResult = applyTournamentRemoteOperation(operatorResult.state, master, {
    type: "relic.set",
    relicId: relic.id,
    selected: true,
    used: true,
  });

  assert.equal(relicResult.state.run.ingot, 31);
  assert.deepEqual(relicResult.state.operators, [operator.id]);
  assert.equal(relicResult.state.operatorCounts[operator.id], 2);
  assert.deepEqual(relicResult.state.relics, [relic.id]);
  assert.deepEqual(relicResult.state.usedRelicIds, [relic.id]);
  assert.deepEqual(relicResult.state.adb, state.adb);
  assert.deepEqual(relicResult.state.preferences, state.preferences);
});

test("batch operation applies all edits as one state transition", async () => {
  const master = await loadMaster();
  const state = baseState();
  const operator = master.operators.find((item) => item.id.startsWith("reserve_"));
  const relic = master.relics.find((item) => item.id);

  const result = applyTournamentRemoteOperation(state, master, {
    type: "batch",
    operations: [
      { type: "run.set", field: "ingot", value: 42 },
      { type: "operator.set", operatorId: operator.id, selected: true, count: 2 },
      { type: "relic.set", relicId: relic.id, selected: true, used: false },
    ],
  });

  assert.equal(result.summary, "3件の入力を一括反映");
  assert.equal(result.state.run.ingot, 42);
  assert.deepEqual(result.state.operators, [operator.id]);
  assert.equal(result.state.operatorCounts[operator.id], 2);
  assert.deepEqual(result.state.relics, [relic.id]);
  assert.equal(state.run.ingot, 7);
  assert.deepEqual(state.operators, []);
});

test("batch operation rejects empty and nested batches", async () => {
  const master = await loadMaster();
  const state = baseState();

  assert.throws(
    () => applyTournamentRemoteOperation(state, master, { type: "batch", operations: [] }),
    /1件以上200件以下/,
  );
  assert.throws(
    () => applyTournamentRemoteOperation(state, master, {
      type: "batch",
      operations: [{ type: "batch", operations: [{ type: "run.clear" }] }],
    }),
    /入れ子/,
  );
});

test("remote operations reject unknown fields and unknown master ids", async () => {
  const master = await loadMaster();
  const state = baseState();

  assert.throws(
    () => applyTournamentRemoteOperation(state, master, {
      type: "run.set",
      field: "adb",
      value: { serial: "internet-controlled" },
    }),
    /許可されていない/,
  );
  assert.throws(
    () => applyTournamentRemoteOperation(state, master, {
      type: "operator.set",
      operatorId: "operator-does-not-exist",
      selected: true,
    }),
    /存在しないオペレーター/,
  );
});

test("remote operations validate squad random effects and clear stale effects when squad changes", async () => {
  const master = await loadMaster();
  const randomSquad = master.squads.find((item) => item.randomEffectOptions?.length);
  const replacementSquad = master.squads.find((item) =>
    item.id !== randomSquad.id && item.campaignId === randomSquad.campaignId);
  const effect = randomSquad.randomEffectOptions[0];
  let state = baseState();

  state = applyTournamentRemoteOperation(state, master, {
    type: "campaign.set",
    campaignId: randomSquad.campaignId,
  }).state;
  state = applyTournamentRemoteOperation(state, master, {
    type: "run.set",
    field: "squadId",
    value: randomSquad.id,
  }).state;
  state = applyTournamentRemoteOperation(state, master, {
    type: "run.set",
    field: "squadRandomEffectOptionId",
    value: effect.id,
  }).state;

  assert.equal(state.run.squadRandomEffectOptionId, effect.id);
  assert.throws(
    () => applyTournamentRemoteOperation(state, master, {
      type: "run.set",
      field: "squadRandomEffectOptionId",
      value: "missing-effect",
    }),
    /現在の分隊に存在しない追加効果/,
  );

  const changed = applyTournamentRemoteOperation(state, master, {
    type: "run.set",
    field: "squadId",
    value: replacementSquad.id,
  });
  assert.equal(changed.state.run.squadRandomEffectOptionId, null);
});

test("special operations normalize effect and operator assignment values", async () => {
  const master = await loadMaster();
  const state = baseState();
  const field = master.campaigns
    .find((item) => item.id === "is3_mizuki")
    .specialFields.find((item) => item.id === "rejectionReaction");
  const effect = master.selectableEffects.find((item) =>
    item.campaignId === "is3_mizuki" && item.slot === field.effectSlot);
  const operator = master.operators.find((item) => item.id);

  const result = applyTournamentRemoteOperation(state, master, {
    type: "special.set",
    field: field.id,
    value: {
      effectId: effect.id,
      operatorTargets: [
        { operatorId: operator.id, instance: 1 },
        { operatorId: operator.id, instance: 2 },
        { operatorId: "missing", instance: 1 },
      ],
    },
  });

  assert.deepEqual(result.state.run.special.is3_mizuki.rejectionReaction, {
    effectId: effect.id,
    operatorIds: [operator.id],
    operatorTargets: [
      { operatorId: operator.id, instance: 1 },
      { operatorId: operator.id, instance: 2 },
    ],
  });
});

test("remote snapshot excludes ADB and local output preferences", async () => {
  const master = await loadMaster();
  const snapshot = buildTournamentRemoteSnapshot(baseState(), master);

  assert.equal(snapshot.state.run.campaignId, "is3_mizuki");
  assert.equal(snapshot.state.adb, undefined);
  assert.equal(snapshot.state.preferences, undefined);
  assert.ok(snapshot.master.campaigns.length >= 1);
  assert.ok(snapshot.master.operators.length >= 1);
  assert.ok(snapshot.master.relics.length >= 1);
});

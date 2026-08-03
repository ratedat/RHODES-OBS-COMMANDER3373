import test from "node:test";
import assert from "node:assert/strict";

import {
  buildDraftState,
  difficultyTierEntries,
  operationKey,
  upsertDraftOperation,
} from "../services/tournament-relay/public/editor-draft.js";

function baseState() {
  return {
    run: {
      campaignId: "is3_mizuki",
      ingot: 1,
      special: { is3_mizuki: {} },
    },
    operators: [],
    operatorCounts: {},
    operatorPromotionLevels: {},
    relics: [],
    usedRelicIds: [],
    bossSelections: {},
  };
}

function master() {
  return {
    difficultyTiers: {
      is5_sarkaz: {
        defaultTierId: "realistic",
        tiers: [
          { id: "realistic", minDifficulty: 0, maxDifficulty: 2 },
          { id: "imaginary", minDifficulty: 9, maxDifficulty: null },
        ],
      },
    },
  };
}

test("editor draft combines run, operator, and relic changes without mutating live state", () => {
  const live = baseState();
  const operations = [
    { type: "run.set", field: "ingot", value: 21 },
    { type: "operator.set", operatorId: "operator-a", selected: true, count: 2 },
    { type: "relic.set", relicId: "relic-a", selected: true, used: true },
  ];

  const draft = buildDraftState(live, operations);

  assert.equal(draft.run.ingot, 21);
  assert.deepEqual(draft.operators, ["operator-a"]);
  assert.equal(draft.operatorCounts["operator-a"], 2);
  assert.deepEqual(draft.relics, ["relic-a"]);
  assert.deepEqual(draft.usedRelicIds, ["relic-a"]);
  assert.equal(live.run.ingot, 1);
  assert.deepEqual(live.operators, []);
});

test("editor draft keeps explicit operator promotion changes and removes them with selection", () => {
  const live = baseState();
  let draft = buildDraftState(live, [{
    type: "operator.set",
    operatorId: "operator-a",
    selected: true,
    count: 1,
    promotionLevel: 2,
  }]);

  assert.equal(draft.operatorPromotionLevels["operator-a"], 2);

  draft = buildDraftState(draft, [{
    type: "operator.set",
    operatorId: "operator-a",
    selected: false,
  }]);
  assert.deepEqual(draft.operatorPromotionLevels, {});
});

test("editor draft replaces a repeated item operation instead of duplicating it", () => {
  let operations = upsertDraftOperation([], {
    type: "operator.set",
    operatorId: "operator-a",
    selected: true,
    count: 1,
  });
  operations = upsertDraftOperation(operations, {
    type: "operator.set",
    operatorId: "operator-a",
    selected: false,
    count: 1,
  });

  assert.equal(operations.length, 1);
  assert.equal(operationKey(operations[0]), "operator:operator-a");
  assert.equal(operations[0].selected, false);
});

test("campaign change drops campaign-dependent pending changes", () => {
  const operations = [
    { type: "run.set", field: "ingot", value: 9 },
    { type: "run.set", field: "squadId", value: "old-squad" },
    { type: "special.set", field: "old-special", value: ["old"] },
    { type: "boss.set", field: "old-boss", value: "old" },
  ];

  const next = upsertDraftOperation(operations, {
    type: "campaign.set",
    campaignId: "is5_sarkaz",
  });

  assert.deepEqual(next.map(operationKey), ["campaign", "run:ingot"]);
});

test("run clear becomes the new draft baseline and later edits are retained", () => {
  let operations = upsertDraftOperation([
    { type: "operator.set", operatorId: "operator-a", selected: true, count: 1 },
  ], { type: "run.clear" });
  operations = upsertDraftOperation(operations, {
    type: "run.set",
    field: "ingot",
    value: 7,
  });

  const draft = buildDraftState(baseState(), operations);
  assert.deepEqual(operations.map(operationKey), ["run:clear", "run:ingot"]);
  assert.equal(draft.run.ingot, 7);
  assert.deepEqual(draft.operators, []);
});

test("editor draft writes canonical squad state and derives difficulty tier", () => {
  const live = baseState();
  live.run.campaignId = "is5_sarkaz";
  live.run.squad = "legacy-squad";
  live.run.squadRandomEffectOptionId = "stale-effect";

  const draft = buildDraftState(live, [
    { type: "run.set", field: "difficulty", value: 9 },
    { type: "run.set", field: "squadId", value: "is5-squad" },
  ], master());

  assert.equal(draft.run.difficulty, 9);
  assert.equal(draft.run.difficultyTierId, "imaginary");
  assert.equal(draft.run.squadId, "is5-squad");
  assert.equal(draft.run.squad, null);
  assert.equal(draft.run.squadRandomEffectOptionId, null);
});

test("difficulty tier entries unwrap canonical definitions and retain legacy arrays", () => {
  assert.deepEqual(
    difficultyTierEntries(master(), "is5_sarkaz").map((item) => item.id),
    ["realistic", "imaginary"],
  );
  assert.deepEqual(
    difficultyTierEntries({ difficultyTiers: { legacy: [{ id: "legacy" }] } }, "legacy"),
    [{ id: "legacy" }],
  );
});

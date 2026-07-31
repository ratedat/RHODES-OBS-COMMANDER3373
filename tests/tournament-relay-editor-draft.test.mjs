import test from "node:test";
import assert from "node:assert/strict";

import {
  buildDraftState,
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
    relics: [],
    usedRelicIds: [],
    bossSelections: {},
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

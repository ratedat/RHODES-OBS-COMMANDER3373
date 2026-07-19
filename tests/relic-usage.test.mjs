import test from "node:test";
import assert from "node:assert/strict";

import {
  prioritizeOwnedRelics,
  supportsRelicUsedFlag,
} from "../app/domain/relic-usage.js";
import {
  clearRelics,
  toggleChoice,
  toggleRelicUsed,
} from "../app/control-actions.js";

test("only run-saving relics support the used flag", () => {
  assert.equal(supportsRelicUsedFlag({ name: "「時の果て」" }), true);
  assert.equal(supportsRelicUsedFlag({ name: "「門」と「救難」" }), true);
  assert.equal(supportsRelicUsedFlag({ name: "特選獣肉缶詰" }), false);
});

test("owned run-saving relics stay first and carry persisted usage", () => {
  const relics = [
    { id: "normal-a", name: "通常A" },
    { id: "gate", name: "「門」と「救難」" },
    { id: "normal-b", name: "通常B" },
    { id: "end", name: "「時の果て」" },
  ];

  const sorted = prioritizeOwnedRelics(relics, ["gate"]);

  assert.deepEqual(sorted.map((item) => item.id), ["gate", "end", "normal-a", "normal-b"]);
  assert.equal(sorted[0].used, true);
  assert.equal(sorted[1].used, false);
  assert.equal(sorted[2].used, false);
});

test("used relic state follows ownership and run clear", () => {
  const state = { relics: ["gate"], usedRelicIds: [] };

  toggleRelicUsed(state, "gate");
  assert.deepEqual(state.usedRelicIds, ["gate"]);

  toggleChoice(state, "relic", "gate");
  assert.deepEqual(state.relics, []);
  assert.deepEqual(state.usedRelicIds, []);

  state.relics = ["gate"];
  state.usedRelicIds = ["gate"];
  clearRelics(state);
  assert.deepEqual(state.relics, []);
  assert.deepEqual(state.usedRelicIds, []);
});

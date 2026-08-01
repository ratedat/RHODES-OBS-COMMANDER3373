import test from "node:test";
import assert from "node:assert/strict";

import {
  mergeRevelationRhetorics,
  normalizeRevelationBoardValue,
} from "../app/domain/special-loadouts.js";
import {
  formatRevelationBoardValue,
  getSelectedSpecialEffectsForField,
} from "../app/domain/special-display.js";

const field = {
  id: "revelation",
  label: "啓示板",
  type: "revelationBoardLoadout",
  effectSlot: "revelationBoard",
  causeGroupLabels: ["本因"],
  structureGroupLabels: ["構成"],
  rhetoricGroupLabels: ["修辞"],
};

const selectableEffectSource = [
  {
    id: "cause-a",
    campaignId: "is4_sami",
    slot: "revelationBoard",
    order: 1,
    groupLabel: "本因",
    name: "本因A",
    effect: "本因の効果",
  },
  {
    id: "structure-a",
    campaignId: "is4_sami",
    slot: "revelationBoard",
    order: 2,
    groupLabel: "構成",
    name: "構成A",
    effect: "構成の効果",
  },
  {
    id: "rhetoric-a",
    campaignId: "is4_sami",
    slot: "revelationBoard",
    order: 3,
    groupLabel: "修辞",
    name: "修辞A",
    effect: "修辞Aの効果",
  },
  {
    id: "rhetoric-b",
    campaignId: "is4_sami",
    slot: "revelationBoard",
    order: 4,
    groupLabel: "修辞",
    name: "修辞B",
    effect: "修辞Bの効果",
  },
];

const selectableEffectMap = new Map(selectableEffectSource.map((item) => [item.id, item]));
const context = { campaignId: "is4_sami", selectableEffectSource, selectableEffectMap };

test("revelation board keeps cause, structure, and rhetoric stacks separate", () => {
  const value = normalizeRevelationBoardValue(field, "is4_sami", {
    causeId: "cause-a",
    structureId: "structure-a",
    rhetorics: [
      { effectId: "rhetoric-a", count: 1 },
      { effectId: "rhetoric-a", count: 2 },
      { effectId: "rhetoric-b", count: 1 },
    ],
  }, selectableEffectSource);

  assert.deepEqual(value, {
    entries: [
      { effectId: "cause-a", stateId: null, slotKind: "cause", count: 1 },
      { effectId: "structure-a", stateId: null, slotKind: "structure", count: 1 },
    ],
    rhetorics: [
      { effectId: "rhetoric-a", count: 3 },
      { effectId: "rhetoric-b", count: 1 },
    ],
  });
});

test("revelation board migrates old stack entries without losing rhetoric effects", () => {
  const value = normalizeRevelationBoardValue(field, "is4_sami", [
    { effectId: "cause-a", count: 1, stateId: "rhetoric-a" },
    { effectId: "structure-a", count: 1, stateId: "rhetoric-b" },
  ], selectableEffectSource);

  assert.deepEqual(value, {
    entries: [
      { effectId: "cause-a", stateId: "rhetoric-a", slotKind: "cause", count: 1 },
      { effectId: "structure-a", stateId: "rhetoric-b", slotKind: "structure", count: 1 },
    ],
  });
});

test("revelation board accepts the paired Avalonia entry shape", () => {
  const value = normalizeRevelationBoardValue(field, "is4_sami", {
    entries: [
      { effectId: "structure-a", stateId: "rhetoric-b", slotKind: "structure", count: 1 },
      { effectId: "cause-a", stateId: "rhetoric-a", slotKind: "cause", count: 1 },
    ],
  }, selectableEffectSource);

  assert.deepEqual(value, {
    entries: [
      { effectId: "structure-a", stateId: "rhetoric-b", slotKind: "structure", count: 1 },
      { effectId: "cause-a", stateId: "rhetoric-a", slotKind: "cause", count: 1 },
    ],
  });
});

test("revelation board selected effects include rhetoric effects as their own entries", () => {
  const effects = getSelectedSpecialEffectsForField(field, {
    revelation: {
      causeId: "cause-a",
      structureId: "structure-a",
      rhetorics: [{ effectId: "rhetoric-a", count: 2 }],
    },
  }, context);

  assert.deepEqual(effects.map((item) => [item.slotLabel, item.name, item.effect]), [
    ["啓示板 構成", "構成A", "構成の効果"],
    ["啓示板 本因", "本因A", "本因の効果"],
    ["啓示板 修辞", "修辞A x2", "修辞Aの効果"],
  ]);
});

test("paired revelation entries are grouped into structure and cause overlays", () => {
  const value = {
    entries: [
      { effectId: "cause-a", stateId: "rhetoric-a", slotKind: "cause", count: 1 },
      { effectId: "structure-a", stateId: "rhetoric-b", slotKind: "structure", count: 2 },
    ],
  };
  const effects = getSelectedSpecialEffectsForField(field, { revelation: value }, context);

  assert.equal(formatRevelationBoardValue(field, value, context), "構成2件 / 本因1件");
  assert.deepEqual(effects.map((item) => [
    item.overlayGroupLabel,
    item.slotLabel,
    item.name,
    item.quantity,
    item.effect,
  ]), [
    ["啓示板・構成", "啓示板 構成", "構成A [修辞B]", 2, "構成の効果 / 修辞 修辞B: 修辞Bの効果"],
    ["啓示板・本因", "啓示板 本因", "本因A [修辞A]", 1, "本因の効果 / 修辞 修辞A: 修辞Aの効果"],
  ]);
});

test("revelation board merges only identical effect rhetoric and slot combinations", () => {
  const value = normalizeRevelationBoardValue(field, "is4_sami", {
    entries: [
      { effectId: "cause-a", stateId: "", slotKind: "cause", count: 1 },
      { effectId: "cause-a", stateId: "", slotKind: "cause", count: 1 },
      { effectId: "cause-a", stateId: "rhetoric-a", slotKind: "cause", count: 1 },
      { effectId: "cause-a", stateId: "rhetoric-a", slotKind: "cause", count: 2 },
      { effectId: "cause-a", stateId: "rhetoric-b", slotKind: "cause", count: 1 },
    ],
  }, selectableEffectSource);

  assert.deepEqual(value, {
    entries: [
      { effectId: "cause-a", stateId: null, slotKind: "cause", count: 2 },
      { effectId: "cause-a", stateId: "rhetoric-a", slotKind: "cause", count: 3 },
      { effectId: "cause-a", stateId: "rhetoric-b", slotKind: "cause", count: 1 },
    ],
  });

  const effects = getSelectedSpecialEffectsForField(field, { revelation: value }, context);
  assert.deepEqual(effects.map((item) => [item.name, item.quantity]), [
    ["本因A", 2],
    ["本因A [修辞A]", 3],
    ["本因A [修辞B]", 1],
  ]);
});

test("revelation board summary shows separate counts", () => {
  const summary = formatRevelationBoardValue(field, {
    causeId: "cause-a",
    structureId: "structure-a",
    rhetorics: mergeRevelationRhetorics([
      { effectId: "rhetoric-a", count: 2 },
      { effectId: "rhetoric-b", count: 1 },
    ]),
  }, context);

  assert.equal(summary, "構成1件 / 本因1件 / 修辞3枚");
});

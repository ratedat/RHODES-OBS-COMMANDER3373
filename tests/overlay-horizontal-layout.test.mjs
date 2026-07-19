import assert from "node:assert/strict";
import test from "node:test";

import { renderOverlayDense } from "../app/components/overlay-layouts.js";
import { renderSpecialOverlayBlock } from "../app/components/special-overlay.js";
import { renderEffectList } from "../app/components/effects.js";
import { getOverlaySpecialEffects } from "../app/domain/special-display.js";

const args = {
  campaign: { id: "is5_sarkaz", number: 5, title: "サルカズの炉辺奇談" },
  squad: { name: "破棘成金分隊" },
  option: null,
  performance: null,
  activeEffects: [
    { type: "秘宝", title: "絵筆", effect: "思考負荷-1" },
    { type: "秘宝", title: "古い記章", effect: "源石錐+2" },
  ],
  relics: [],
  operators: [],
  specialFields: [],
  special: {},
  difficultyGrade: { label: "魂に直面 18" },
  run: {},
};

const context = {
  mode: "manual",
  getSpecialTags: () => [],
  runStatDisplayItems: () => [],
  getOverlaySpecialEffects: () => [],
  getBossFlagEntries: () => [
    { id: "floor3", floor: 3, title: "3層ボス" },
    { id: "floor5", floor: 5, title: "5層ボス" },
  ],
  renderBossChip: (entry) => `<span class="boss-chip" data-boss-id="${entry.id}">${entry.title}</span>`,
  getDifficultyTierLabel: () => "現実的",
  renderSpecialOverlayBlock: () => "",
  getOverlayScrollSpeed: () => 12,
  renderEffectList,
  relicEffectForDisplay: () => "",
};

test("horizontal overlay uses a broadcast rail with inline bosses and a full-width effect row", () => {
  const output = renderOverlayDense({ ...args, orientation: "horizontal" }, context);

  assert.match(output, /class="stream-overlay-shell stream-horizontal stream-broadcast"/);
  assert.match(output, /class="stream-broadcast-status"/);
  assert.match(output, /class="stream-panel stream-boss-panel"/);
  assert.match(output, /class="stream-boss-strip"/);
  assert.match(output, /data-boss-id="floor3"/);
  assert.match(output, /data-boss-id="floor5"/);
  assert.match(output, /class="stream-panel stream-horizontal-effect-panel"/);
  assert.match(output, /effect-list stream-horizontal-effect-list/);
  assert.match(output, /思考負荷-1/);
  assert.match(output, /源石錐\+2/);
});

test("vertical overlay keeps bosses and effects inside the run panel", () => {
  const output = renderOverlayDense({ ...args, orientation: "vertical" }, context);

  assert.doesNotMatch(output, /stream-boss-panel/);
  assert.doesNotMatch(output, /stream-horizontal-effect-panel/);
  assert.match(output, /class="stream-scroll stream-effect-scroll"/);
  assert.match(output, /data-boss-id="floor3"/);
});

test("horizontal overlay renders selected Sarkaz thoughts with image and count", () => {
  const thought = {
    id: "thought-a",
    name: "築壁",
    slot: "thought",
    slotLabel: "思案",
    groupLabel: "妙想",
    effect: "防御効果",
    image: { localPath: "assets/thought/thought-a.png" },
  };
  const specialFields = [{
    id: "thought",
    label: "思案",
    type: "effectStackLoadout",
    effectSlot: "thought",
    unitLabel: "個",
    hideStateInput: true,
    overlayToggle: true,
    overlayDefaultVisible: true,
  }];
  const special = {
    thought: [{ effectId: thought.id, count: 2, stateId: null }],
    thoughtOverlayVisible: true,
  };
  const displayContext = {
    campaignId: "is5_sarkaz",
    selectableEffectMap: new Map([[thought.id, thought]]),
    selectableEffectSource: [thought],
  };
  const output = renderOverlayDense({
    ...args,
    specialFields,
    special,
    orientation: "horizontal",
  }, {
    ...context,
    getOverlaySpecialEffects: (_campaignId, fields, values) => getOverlaySpecialEffects(fields, values, displayContext),
    renderSpecialOverlayBlock: (items, mode, speedKey) => renderSpecialOverlayBlock(items, mode, speedKey, () => 12),
  });

  assert.match(output, /class="stream-special-section"/);
  assert.match(output, /src="\/assets\/thought\/thought-a\.png"/);
  assert.match(output, /築壁 x2/);
  assert.match(output, /思案 \/ 妙想/);
});

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

test("operator names expose the Mizuki rejection target class", () => {
  const output = renderOverlayDense({
    ...args,
    operators: [{
      id: "kroos",
      name: "クルース",
      rarity: 3,
      class: "狙撃",
      isRejectionReactionTarget: true,
    }],
    orientation: "horizontal",
  }, context);

  assert.match(output, /class="rejection-reaction-operator-name">クルース/);
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

test("special overlay renders coin effect groups and visible effect text", () => {
  const output = renderSpecialOverlayBlock([
    {
      id: "coin-active",
      name: "志遂げんと欲す x1 / 存護",
      slotLabel: "有効銭",
      activationLabel: "発動中",
      overlayGroupId: "activeCoins-active",
      overlayGroupLabel: "有効銭（振出中）",
      effect: "味方の攻撃速度+80",
      quantity: 1,
    },
    {
      id: "coin-held",
      name: "門と救難 x2",
      slotLabel: "保有銭",
      activationLabel: "待機",
      overlayGroupId: "coins-held",
      overlayGroupLabel: "保有銭（銭匣内）",
      effect: "振出時: 異境の入口が出現",
      quantity: 2,
    },
  ], "part", "verticalRelicScrollSpeed", () => 12);

  assert.match(output, /special-overlay-group-activeCoins-active/);
  assert.match(output, /有効銭（振出中）/);
  assert.match(output, /保有銭（銭匣内）/);
  assert.match(output, /味方の攻撃速度\+80/);
  assert.match(output, /振出時: 異境の入口が出現/);
  assert.match(output, />2枚</);
});

test("Mizuki special overlay renders distinct horde, rejection, and revelation groups", () => {
  const output = renderSpecialOverlayBlock([
    {
      id: "horde",
      name: "呼び声：探索",
      slotLabel: "大群の呼び声",
      overlayGroupId: "mizuki-horde-calls",
      overlayGroupLabel: "大群の呼び声",
      overlayGroupUnit: "件",
      effect: "灯火の減少が2倍になる",
    },
    {
      id: "rejection",
      name: "散漫と異変 / 対象2名",
      slotLabel: "拒絶反応",
      overlayGroupId: "mizuki-rejection",
      overlayGroupLabel: "拒絶反応",
      overlayGroupUnit: "件",
      effect: "最大HP・攻撃力・防御力-50%",
    },
    {
      id: "revelation",
      name: "ウルサスの怒号",
      slotLabel: "啓示",
      overlayGroupId: "mizuki-revelations",
      overlayGroupLabel: "啓示",
      overlayGroupUnit: "件",
      effect: "敵全体の攻撃力-30%",
    },
  ], "part", "verticalRelicScrollSpeed", () => 12);

  assert.match(output, /special-overlay-group-mizuki-horde-calls/);
  assert.match(output, /special-overlay-group-mizuki-rejection/);
  assert.match(output, /special-overlay-group-mizuki-revelations/);
  assert.match(output, /大群の呼び声/);
  assert.match(output, /拒絶反応/);
  assert.match(output, /啓示/);
  assert.equal((output.match(/>1件</g) || []).length, 3);
});

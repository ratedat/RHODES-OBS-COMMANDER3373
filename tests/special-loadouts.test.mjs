import test from "node:test";
import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";

import {
  addCoinEntry,
  addSpecialEffect,
  updateCoinEntryStatus,
} from "../app/control-actions.js";
import { mergeEffectStackEntries } from "../app/domain/special-loadouts.js";
import {
  formatCoinLoadoutValue,
  formatSpecialValue,
  getOverlaySpecialEffects,
  getSelectedSpecialEffectsForField,
} from "../app/domain/special-display.js";
import { mergeCoinEntries } from "../app/domain/special-values.js";
import { renderSpecialEffectSelectOptions } from "../app/components/special-controls.js";

const coinMap = new Map([
  ["coin-a", { id: "coin-a", name: "通宝A", effect: "通宝効果" }],
  ["status-a", { id: "status-a", name: "錆色", effect: "状態効果A" }],
  ["status-b", { id: "status-b", name: "存護", effect: "状態効果B" }],
]);

test("IS#3 revelation effects can be added through the generic add action", () => {
  const state = { run: { special: { is3_mizuki: {} } } };

  addSpecialEffect(state, "is3_mizuki", "revelations", "is3_mizuki_selectable_revelation_mcasci1");
  addSpecialEffect(state, "is3_mizuki", "revelations", "is3_mizuki_selectable_revelation_mcasci1");
  addSpecialEffect(state, "is3_mizuki", "revelations", "is3_mizuki_selectable_revelation_mcasci2");

  assert.deepEqual(state.run.special.is3_mizuki.revelations, [
    "is3_mizuki_selectable_revelation_mcasci1",
    "is3_mizuki_selectable_revelation_mcasci2",
  ]);
});

test("coin loadouts merge the same coin and keep different statuses separate", () => {
  const entries = mergeCoinEntries([
    { coinId: "coin-a", count: 1, statusId: null, face: "front" },
    { coinId: "coin-a", count: 2, statusId: "status-a", face: "front" },
    { coinId: "coin-a", count: 3, statusId: "status-a", face: "back" },
  ]);

  assert.deepEqual(entries, [
    { coinId: "coin-a", count: 1, statusId: null },
    { coinId: "coin-a", count: 5, statusId: "status-a" },
  ]);
});

test("adding a coin with a selected status creates a visible second slot", () => {
  const state = { run: { special: { is6_sui: {} } } };

  addCoinEntry(state, "is6_sui", "coins", { coinId: "coin-a", count: 1, statusId: null, face: "front" });
  addCoinEntry(state, "is6_sui", "coins", { coinId: "coin-a", count: 1, statusId: "status-a", face: "front" });

  assert.deepEqual(state.run.special.is6_sui.coins, [
    { coinId: "coin-a", count: 1, statusId: null },
    { coinId: "coin-a", count: 1, statusId: "status-a" },
  ]);

  assert.equal(formatCoinLoadoutValue({ id: "coins" }, state.run.special.is6_sui.coins, { selectableEffectMap: coinMap }), "2枚 / 2枠");
});

test("changing a coin status preserves a separate status row unless the exact slot already exists", () => {
  const state = {
    run: {
      special: {
        is6_sui: {
          coins: [
            { coinId: "coin-a", count: 1, statusId: null, face: "front" },
            { coinId: "coin-a", count: 1, statusId: "status-a", face: "front" },
          ],
        },
      },
    },
  };

  updateCoinEntryStatus(state, "is6_sui", "coins", 0, "status-b");

  assert.deepEqual(state.run.special.is6_sui.coins, [
    { coinId: "coin-a", count: 1, statusId: "status-b" },
    { coinId: "coin-a", count: 1, statusId: "status-a" },
  ]);
});

test("select labels include group context for same named future effects", () => {
  const rendered = renderSpecialEffectSelectOptions([
    { id: "a", name: "同名", groupLabel: "通常", slotLabel: "通宝" },
    { id: "b", name: "同名", groupLabel: "特殊", slotLabel: "通宝" },
  ]);

  assert.match(rendered, />通常 \/ 同名<\/option>/);
  assert.match(rendered, />特殊 \/ 同名<\/option>/);
});

test("coin overlay separates rolled and held effects while marking waiting coins", () => {
  const mixedCoin = {
    id: "coin-mixed",
    name: "複合通宝",
    effect: "銭匣内にある場合、指揮経験値+30%。振り出されると、敵の攻撃力+30%",
  };
  const waitingCoin = {
    id: "coin-waiting",
    name: "待機通宝",
    effect: "振り出されると、味方の攻撃速度+20",
  };
  const selectableEffectMap = new Map([
    [mixedCoin.id, mixedCoin],
    [waitingCoin.id, waitingCoin],
  ]);
  const context = {
    campaignId: "is6_sui",
    selectableEffectMap,
    selectableEffectSource: [...selectableEffectMap.values()],
  };

  const active = getSelectedSpecialEffectsForField({
    id: "activeCoins",
    label: "有効銭",
    type: "coinLoadout",
    overlayEffectScope: "active",
  }, { activeCoins: [{ coinId: mixedCoin.id, count: 1 }] }, context);
  assert.equal(active[0].overlayGroupId, "activeCoins-active");
  assert.equal(active[0].overlayGroupLabel, "有効銭（振出中）");
  assert.equal(active[0].activationLabel, "発動中");
  assert.doesNotMatch(active[0].effect, /銭匣内にある場合/);
  assert.match(active[0].effect, /敵の攻撃力\+30%/);

  const held = getSelectedSpecialEffectsForField({
    id: "coins",
    label: "保有銭",
    type: "coinLoadout",
    overlayEffectScope: "held",
  }, {
    coins: [
      { coinId: mixedCoin.id, count: 1 },
      { coinId: waitingCoin.id, count: 2 },
    ],
  }, context);
  assert.equal(held[0].overlayGroupId, "coins-held");
  assert.equal(held[0].overlayGroupLabel, "保有銭（銭匣内）");
  assert.equal(held[0].activationLabel, "在匣");
  assert.match(held[0].effect, /指揮経験値\+30%/);
  assert.doesNotMatch(held[0].effect, /振り出されると/);
  assert.equal(held[1].overlayGroupId, "coins-held");
  assert.equal(held[1].overlayGroupLabel, "保有銭（銭匣内）");
  assert.equal(held[1].activationLabel, "待機");
  assert.match(held[1].effect, /^次回条件:/);
});

test("IS#5 thought field uses countable effect stack entries without state slots", () => {
  const campaigns = JSON.parse(readFileSync(new URL("../data/campaigns.json", import.meta.url), "utf8"));
  const campaign = campaigns.find((item) => item.id === "is5_sarkaz");
  const field = campaign.specialFields.find((item) => item.id === "thought");

  assert.equal(field.type, "effectStackLoadout");
  assert.equal(field.effectSlot, "thought");
  assert.equal(field.hideStateInput, true);
  assert.equal(field.unitLabel, "個");
  assert.equal(field.overlayDefaultVisible, true);

  assert.deepEqual(mergeEffectStackEntries(field, ["thought-a", "thought-a", { effectId: "thought-b", count: 3 }], campaign.id), [
    { effectId: "thought-a", count: 2, stateId: null },
    { effectId: "thought-b", count: 3, stateId: null },
  ]);
});

test("IS#6 coin fields are visible in the special OBS output by default", () => {
  const campaigns = JSON.parse(readFileSync(new URL("../data/campaigns.json", import.meta.url), "utf8"));
  const campaign = campaigns.find((item) => item.id === "is6_sui");
  const activeCoins = campaign.specialFields.find((item) => item.id === "activeCoins");
  const heldCoins = campaign.specialFields.find((item) => item.id === "coins");

  assert.equal(activeCoins.overlayToggle, true);
  assert.equal(activeCoins.overlayDefaultVisible, true);
  assert.equal(activeCoins.overlayEffectScope, "active");
  assert.equal(heldCoins.overlayToggle, true);
  assert.equal(heldCoins.overlayDefaultVisible, true);
  assert.equal(heldCoins.overlayEffectScope, "held");
});

test("IS#6 support martial keeps multiple manual effects in state and OBS output", () => {
  const campaigns = JSON.parse(readFileSync(new URL("../data/campaigns.json", import.meta.url), "utf8"));
  const campaign = campaigns.find((item) => item.id === "is6_sui");
  const field = campaign.specialFields.find((item) => item.id === "supportMartial");
  const special = {
    supportMartial: ["配置時に攻撃速度+20", "初回配置コスト-3", "配置時に攻撃速度+20"],
  };

  assert.equal(field.type, "textMultiSelect");
  assert.equal(field.overlayToggle, true);
  assert.equal(field.overlayDefaultVisible, true);

  const effects = getSelectedSpecialEffectsForField(field, special, {
    campaignId: campaign.id,
    selectableEffectMap: new Map(),
    selectableEffectSource: [],
  });
  assert.deepEqual(effects.map((item) => item.name), ["配置時に攻撃速度+20", "初回配置コスト-3"]);
  assert.deepEqual(effects.map((item) => item.slotLabel), ["支武", "支武"]);
});

test("Mizuki operator assignments use human-readable OBS labels", () => {
  const reactionId = "is3_mizuki_selectable_rejectionReaction_mcasci24";
  const selectableEffectMap = new Map([
    [reactionId, { id: reactionId, name: "造血障害" }],
  ]);
  const context = { selectableEffectMap };

  const rejection = formatSpecialValue(
    { id: "rejectionReaction", type: "operatorEffectAssignment" },
    {
      effectId: reactionId,
      operatorIds: ["kroos", "reserve_defender"],
      operatorTargets: [
        { operatorId: "kroos", instance: 1 },
        { operatorId: "reserve_defender", instance: 1 },
        { operatorId: "reserve_defender", instance: 2 },
      ],
    },
    context,
  );
  const evolution = formatSpecialValue(
    { id: "operatorEvolution", type: "operatorMultiSelect" },
    {
      operatorIds: ["durin", "reserve_defender"],
      operatorTargets: [
        { operatorId: "durin", instance: 1 },
        { operatorId: "reserve_defender", instance: 2 },
      ],
    },
    context,
  );

  assert.equal(rejection, "造血障害 / 対象3名");
  assert.equal(evolution, "対象2名");
  assert.doesNotMatch(`${rejection} ${evolution}`, /\[object Object\]/);
});

test("Mizuki OBS output separates horde calls, rejection reactions, and revelations", () => {
  const campaigns = JSON.parse(readFileSync(new URL("../data/campaigns.json", import.meta.url), "utf8"));
  const effectData = JSON.parse(readFileSync(new URL("../data/selectable-effects.json", import.meta.url), "utf8"));
  const campaign = campaigns.find((item) => item.id === "is3_mizuki");
  const fields = campaign.specialFields.filter((field) =>
    ["hordeCalls", "rejectionReaction", "revelations"].includes(field.id));
  const selectableEffectMap = new Map(effectData.selectableEffects.map((item) => [item.id, item]));
  const context = {
    campaignId: campaign.id,
    selectableEffectMap,
    selectableEffectSource: effectData.selectableEffects,
  };
  const special = {
    hordeCalls: ["is3_mizuki_selectable_hordeCall_mcasci12"],
    rejectionReaction: {
      effectId: "is3_mizuki_selectable_rejectionReaction_mcasci24",
      operatorTargets: [
        { operatorId: "kroos", instance: 1 },
        { operatorId: "reserve_defender", instance: 1 },
      ],
    },
    revelations: ["is3_mizuki_selectable_revelation_mcasci1"],
    hordeCallsOverlayVisible: true,
    rejectionReactionOverlayVisible: true,
  };

  assert.deepEqual(fields.map((field) => [field.id, field.overlayToggle, field.overlayDefaultVisible]), [
    ["rejectionReaction", true, true],
    ["revelations", true, true],
    ["hordeCalls", true, true],
  ]);

  const effects = getOverlaySpecialEffects(fields, special, context);
  assert.deepEqual(effects.map((item) => item.overlayGroupLabel), [
    "拒絶反応",
    "啓示",
    "大群の呼び声",
  ]);
  assert.deepEqual(effects.map((item) => item.overlayGroupId), [
    "mizuki-rejection",
    "mizuki-revelations",
    "mizuki-horde-calls",
  ]);
  assert.deepEqual(effects.map((item) => item.overlayGroupUnit), ["件", "件", "件"]);
  assert.equal(effects[0].name, "散漫と異変 / 対象2名");

  const hiddenRevelations = getOverlaySpecialEffects(fields, {
    ...special,
    revelationsOverlayVisible: false,
  }, context);
  assert.deepEqual(hiddenRevelations.map((item) => item.overlayGroupLabel), [
    "拒絶反応",
    "大群の呼び声",
  ]);

  const localImageEffects = effectData.selectableEffects.filter((item) =>
    item.campaignId === campaign.id && ["rejectionReaction", "revelation"].includes(item.slot));
  assert.equal(localImageEffects.filter((item) => item.slot === "revelation").length, 7);
  assert.equal(localImageEffects.filter((item) => item.slot === "rejectionReaction").length, 10);
  assert.equal(localImageEffects.every((item) => item.image?.localPath), true);
  assert.equal(localImageEffects.every((item) => existsSync(new URL(`../${item.image.localPath}`, import.meta.url))), true);
});

test("IS#6 seasonal hours include all normal and awakened variants", () => {
  const data = JSON.parse(readFileSync(new URL("../data/selectable-effects.json", import.meta.url), "utf8"));
  const seasonalHours = data.selectableEffects.filter((item) =>
    item.campaignId === "is6_sui" && item.slot === "seasonalHours");
  const awakened = seasonalHours.filter((item) => item.variantRank === "awakening");

  assert.equal(seasonalHours.length, 48);
  assert.equal(awakened.length, 12);
  assert.equal(awakened.find((item) => item.parentName === "巳農").effect, "最初に配置するオペレーターの配置コスト-6");
});

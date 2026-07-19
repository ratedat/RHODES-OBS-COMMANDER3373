import assert from "node:assert/strict";
import test from "node:test";

import {
  renderOverlayCompact,
  renderOverlayDefault,
  renderOverlayDense,
} from "../app/components/overlay-layouts.js";
import { renderOverlayPart } from "../app/components/overlay-parts.js";

const relics = [
  {
    id: "is3_mizuki_relic_228",
    name: "「時の果て」",
    used: true,
    image: { localPath: "assets/relic/time-end.png" },
  },
  {
    id: "is3_mizuki_relic_001",
    name: "未使用秘宝",
    used: false,
    image: { localPath: "assets/relic/unused.png" },
  },
];

const args = {
  campaign: { id: "is3_mizuki", number: 3, title: "ミヅキと紺碧の樹", fullTitle: "統合戦略" },
  squad: null,
  option: null,
  performance: null,
  activeEffects: [],
  relics,
  operators: [],
  specialFields: [],
  special: {},
  difficultyGrade: null,
  run: {},
  runDifficulty: null,
  updatedAt: "2026-07-20T00:00:00Z",
  bossFlagCount: 0,
};

const context = {
  mode: "manual",
  getSpecialTags: () => [],
  runStatDisplayItems: () => [],
  getOverlaySpecialEffects: () => [],
  getBossFlagEntries: () => [],
  getDifficultyTierLabel: () => "未選択",
  renderSpecialOverlayBlock: () => "",
  getOverlayScrollSpeed: () => 12,
  renderEffectList: () => "",
  relicEffectForDisplay: (item) => item.name,
  renderBossChip: () => "",
  renderBossCard: () => "",
};

function assertOneUsedIcon(output, label) {
  assert.equal((output.match(/class="relic-used-badge"/g) || []).length, 1, `${label}: badge count`);
  assert.match(output, /aria-label="使用済"/u, `${label}: accessible label`);
  assert.match(output, /relic-used-icon/u, `${label}: stable icon element`);
}

test("every full overlay layout renders the used relic icon", () => {
  assertOneUsedIcon(renderOverlayCompact(args, context), "compact");
  assertOneUsedIcon(renderOverlayDense({ ...args, orientation: "vertical" }, context), "vertical");
  assertOneUsedIcon(renderOverlayDense({ ...args, orientation: "horizontal" }, context), "horizontal");
  assertOneUsedIcon(renderOverlayDefault(args, context), "default");
});

test("the standalone relic overlay part renders the used relic icon", () => {
  assertOneUsedIcon(renderOverlayPart("relics", args, context), "relic part");
});

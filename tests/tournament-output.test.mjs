import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { renderOverlayPart } from "../app/components/overlay-parts.js";
import {
  filterTournamentOperators,
  normalizeTournamentInfo,
  normalizeTournamentOperatorRarities,
} from "../app/domain/tournament-output.js";

const context = {
  mode: "大会",
  getSpecialTags: () => [],
  runStatDisplayItems: () => [],
  getOverlaySpecialEffects: () => [],
  getBossFlagEntries: () => [],
  getDifficultyTierLabel: () => "未選択",
  renderSpecialOverlayBlock: () => "",
  getOverlayScrollSpeed: () => 12,
  renderEffectList: () => "",
  relicEffectForDisplay: (item) => item.name,
  renderBossCard: () => "",
};

const baseArgs = {
  campaign: { id: "is5_sarkaz", number: 5, title: "サルカズの炉辺奇談" },
  squad: null,
  option: null,
  performance: null,
  activeEffects: [],
  relics: [],
  operators: [],
  specialFields: [],
  special: {},
  difficultyGrade: null,
  run: {},
};

test("tournament values are bounded without inventing missing values", () => {
  assert.deepEqual(normalizeTournamentInfo(null), {
    score: null,
    withdrawals: null,
    memo: "",
  });
  assert.deepEqual(normalizeTournamentInfo({
    score: "2000000",
    withdrawals: "-3",
    memo: `  ${"大会メモ".repeat(80)}  `,
  }), {
    score: 999999,
    withdrawals: 0,
    memo: "大会メモ".repeat(40),
  });
});

test("operator rarity visibility accepts only Arknights rarities in descending order", () => {
  assert.deepEqual(normalizeTournamentOperatorRarities(undefined), [6, 5, 4, 3, 2, 1]);
  assert.deepEqual(normalizeTournamentOperatorRarities([4, "6", 4, 0, 7]), [6, 4]);
  assert.deepEqual(filterTournamentOperators([
    { id: "six", rarity: 6 },
    { id: "five", rarity: 5 },
    { id: "four", rarity: 4 },
  ], [6]), [{ id: "six", rarity: 6 }]);
});

test("operator part can render only six-star icons without leaking hidden names", () => {
  const output = renderOverlayPart("operators", {
    ...baseArgs,
    presentation: {
      relicIconOnly: false,
      operatorIconOnly: true,
      operatorRarities: [6],
    },
    operators: [
      { id: "six", name: "六星", rarity: 6, class: "前衛", branch: "勇士", image: {} },
      { id: "five", name: "五星", rarity: 5, class: "医療", branch: "医師", image: {} },
    ],
  }, context);

  assert.match(output, /overlay-operator-icon-only/);
  assert.match(output, /aria-label="六星"/);
  assert.match(output, /title="六星"/);
  assert.doesNotMatch(output, /五星/);
});

test("rarity filtering keeps the run roster count while hiding lower-rarity cards", () => {
  const output = renderOverlayPart("status", {
    ...baseArgs,
    presentation: {
      operatorIconOnly: true,
      operatorRarities: [6],
    },
    operators: [
      { id: "six", name: "六星", rarity: 6 },
      { id: "five", name: "五星", rarity: 5 },
    ],
  }, context);

  assert.match(output, /<span>招集<\/span><strong>2<\/strong>/);
});

test("relic part exposes icon-only presentation while preserving labels for assistive use", () => {
  const output = renderOverlayPart("relics", {
    ...baseArgs,
    presentation: {
      relicIconOnly: true,
      operatorIconOnly: false,
      operatorRarities: [6, 5, 4, 3, 2, 1],
    },
    relics: [{ id: "relic", name: "知識の橋", image: {} }],
  }, context);

  assert.match(output, /overlay-relic-icon-only/);
  assert.match(output, /aria-label="知識の橋"/);
  assert.match(output, /title="知識の橋"/);
});

test("tournament information has its own safe overlay part", () => {
  const output = renderOverlayPart("tournament", {
    ...baseArgs,
    run: {
      tournamentInfo: {
        score: 1280,
        withdrawals: 3,
        memo: "決勝 <script>alert(1)</script>",
      },
    },
  }, context);

  assert.match(output, /data-tournament-field="score"[^>]*>1280</);
  assert.match(output, /data-tournament-field="withdrawals"[^>]*>3</);
  assert.match(output, /決勝 &lt;script&gt;alert\(1\)&lt;\/script&gt;/);
  assert.doesNotMatch(output, /<script>/);
});

test("icon-only CSS collapses labels instead of leaving empty wide cards", async () => {
  const styles = await readFile(new URL("../app/styles.css", import.meta.url), "utf8");
  assert.match(styles, /\.overlay-relic-icon-only\s+\.overlay-part-relic-grid\s*\{[^}]*minmax\(42px,\s*56px\)/s);
  assert.match(styles, /\.overlay-operator-icon-only\s+\.overlay-part-operator-grid\s*\{[^}]*minmax\(38px,\s*48px\)/s);
  assert.match(styles, /\.overlay-operator-icon-only\s+\.overlay-part-operator\s*>\s*div:not\(\.operator-portrait\)\s*\{[^}]*display:\s*none/s);
});

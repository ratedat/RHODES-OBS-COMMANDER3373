import assert from "node:assert/strict";
import test from "node:test";
import {
  buildOperatorCatalogView,
  buildRelicCatalogView,
} from "../services/tournament-relay/public/catalog-filters.js";

const operators = [
  { id: "vanguard", name: "先鋒A", rarity: 6, class: "先鋒", branch: "旗手", displayOrder: 2 },
  { id: "sniper", name: "狙撃A", rarity: 5, class: "狙撃", branch: "速射手", displayOrder: 1 },
  { id: "caster", name: "術師A", rarity: 4, class: "術師", branch: "中堅術師", displayOrder: 3 },
  { id: "hidden", name: "未実装", rarity: 6, class: "前衛", branch: "勇士", hiddenByDefault: true },
];

test("operator catalog filters cascade rarity, class and branch options in game order", () => {
  const view = buildOperatorCatalogView(operators, [], {
    rarity: "5",
    className: "狙撃",
    branch: "速射手",
  });

  assert.deepEqual(view.options.rarity, [6, 5, 4]);
  assert.deepEqual(view.options.classes, ["狙撃"]);
  assert.deepEqual(view.options.branches, ["速射手"]);
  assert.deepEqual(view.items.map((item) => item.id), ["sniper"]);
  assert.equal(view.total, 3);
});

test("operator catalog supports search, selected-only, selected-first and display columns", () => {
  const selectedOnly = buildOperatorCatalogView(operators, ["caster"], {
    search: "中堅 術師",
    selectedOnly: true,
    columns: 9,
  });
  assert.deepEqual(selectedOnly.items.map((item) => item.id), ["caster"]);
  assert.equal(selectedOnly.filters.columns, 4);

  const selectedFirst = buildOperatorCatalogView(operators, ["caster"], {
    selectedFirst: true,
    sort: "rarity",
  });
  assert.deepEqual(selectedFirst.items.map((item) => item.id), ["caster", "vanguard", "sniper"]);
});

test("relic catalog scopes campaign and supports category, number and selected filters", () => {
  const relics = [
    { id: "r2", campaignId: "is5", number: 20, name: "乙", category: "B", effect: "防御" },
    { id: "r1", campaignId: "is5", number: 3, name: "甲", category: "A", effect: "攻撃" },
    { id: "r3", campaignId: "is5", number: 1, name: "丙", category: "A", effect: "回復" },
    { id: "other", campaignId: "is6", number: 1, name: "別テーマ", category: "A" },
  ];
  const view = buildRelicCatalogView(relics, ["r2"], "is5", {
    category: "A",
    sort: "number",
  });
  assert.deepEqual(view.options.categories, ["A", "B"]);
  assert.deepEqual(view.items.map((item) => item.id), ["r3", "r1"]);
  assert.equal(view.total, 3);

  const selected = buildRelicCatalogView(relics, ["r2"], "is5", {
    selectedOnly: true,
    search: "防 御",
  });
  assert.deepEqual(selected.items.map((item) => item.id), ["r2"]);
});

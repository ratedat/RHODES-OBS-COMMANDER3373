import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { renderRelicControlRow } from "../app/components/choice-cards.js";
import { updateRelicStackCount } from "../app/control-actions.js";
import {
  normalizeManualRelicStackCount,
  normalizeRelicStackCounts,
  relicIsExplicitlyNonStack,
  relicStackMaximum,
  relicSupportsStackCount,
} from "../app/domain/relic-stacks.js";

const stackData = JSON.parse(await readFile(new URL("../data/relic-stack-rules.json", import.meta.url), "utf8"));
const relicData = JSON.parse(await readFile(new URL("../data/relics.json", import.meta.url), "utf8"));
const rules = stackData.rules;

test("stack relic rules remain unique and match canonical relic data", () => {
  const canonical = new Map(relicData.relics.map((item) => [item.id, item]));

  assert.equal(stackData.schemaVersion, 1);
  assert.equal(rules.length, 31);
  assert.equal(new Set(rules.map((rule) => rule.relicId)).size, 31);
  for (const rule of rules) {
    assert.equal(canonical.get(rule.relicId)?.campaignId, rule.campaignId, rule.relicId);
    assert.equal(canonical.get(rule.relicId)?.name, rule.name, rule.relicId);
  }
  assert.deepEqual(stackData.nonStackRelics, [{
    relicId: "is6_sui_relic_099",
    campaignId: "is6_sui",
    name: "賞善郎",
    reason: "Confirmed non-stack relic; its card artwork can be mistaken for a stack badge.",
  }]);
  assert.equal(canonical.get("is6_sui_relic_099")?.name, "賞善郎");
  assert.equal(relicStackMaximum("is5_sarkaz_relic_287", rules), 10);
  assert.equal(relicStackMaximum("is3_mizuki_relic_261", rules), null);
});

test("state normalization keeps only allowlisted stack relic counts", () => {
  const normalized = normalizeRelicStackCounts({
    is5_sarkaz_relic_287: 11,
    is3_mizuki_relic_261: 123,
    unlisted_relic: 44,
    is6_sui_relic_099: 2,
    unowned_relic: 5,
  }, ["is5_sarkaz_relic_287", "is3_mizuki_relic_261", "unlisted_relic", "is6_sui_relic_099"], stackData);

  assert.deepEqual(normalized, {
    is3_mizuki_relic_261: 123,
  });
});

test("manual stack input clamps known maxima and removes zero", () => {
  assert.equal(normalizeManualRelicStackCount(12, "is5_sarkaz_relic_287", rules), 10);
  assert.equal(normalizeManualRelicStackCount(123, "is3_mizuki_relic_261", rules), 123);
  assert.equal(normalizeManualRelicStackCount(0, "is5_sarkaz_relic_287", rules), 0);
  assert.equal(normalizeManualRelicStackCount(44, "unlisted_relic", rules), 0);
  assert.equal(normalizeManualRelicStackCount(2, "is6_sui_relic_099", stackData), 0);
  assert.equal(relicIsExplicitlyNonStack("is6_sui_relic_099", stackData), true);
  assert.equal(relicSupportsStackCount("unlisted_relic", { unlisted_relic: 44 }, stackData), false);
  assert.equal(relicSupportsStackCount("is6_sui_relic_099", { is6_sui_relic_099: 2 }, stackData), false);

  const state = { relics: ["is5_sarkaz_relic_287"], relicStackCounts: {} };
  updateRelicStackCount(state, "is5_sarkaz_relic_287", 12, 10);
  assert.deepEqual(state.relicStackCounts, { is5_sarkaz_relic_287: 10 });
  updateRelicStackCount(state, "is5_sarkaz_relic_287", 0, 10);
  assert.deepEqual(state.relicStackCounts, {});
});

test("relic control row exposes an accessible stack selector with the configured maximum", () => {
  const output = renderRelicControlRow(
    { id: "is5_sarkaz_relic_287", number: 287, name: "呪儀の溯獣", category: "儀式用品", image: {} },
    true,
    "効果",
    { supportsStackCount: true, stackCount: 9, stackMaximum: 10 },
  );

  assert.match(output, /data-relic-stack-count="is5_sarkaz_relic_287"/);
  assert.match(output, /min="0" max="10" value="9"/);
  assert.match(output, /呪儀の溯獣のスタック数/);
  assert.match(output, /上限10/);
});

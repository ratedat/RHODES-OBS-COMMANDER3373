import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";

import {
  createNameCorrectionResolver,
  normalizeNameCorrectionText,
} from "../app/domain/recognition/name-corrections.js";
import { createOperatorCandidateExtractor } from "../app/domain/recognition/operator-candidate-extractor.js";
import { createRelicCandidateExtractor } from "../app/domain/recognition/relic-candidate-extractor.js";
import { createThoughtCandidateExtractor } from "../app/domain/recognition/thought-candidate-extractor.js";

async function readJson(relativePath) {
  const url = new URL(`../${relativePath}`, import.meta.url);
  return JSON.parse(await fs.readFile(url, "utf8"));
}

test("name correction normalization follows the shared NFKC compact quote contract", () => {
  assert.equal(normalizeNameCorrectionText(" Ｗ 「 A・B-1 」 "), "wa・b-1");
  assert.equal(normalizeNameCorrectionText("『帰還』"), "帰還");
});

test("the shared correction master resolves every declared alias to a validated catalog target", async () => {
  const [nameCorrections, operatorPayload, relicPayload, effectPayload] = await Promise.all([
    readJson("data/recognition/rhodes-name-corrections.json"),
    readJson("data/operators.json"),
    readJson("data/relics.json"),
    readJson("data/selectable-effects.json"),
  ]);
  const catalogs = {
    operator: operatorPayload.operators,
    relic: relicPayload.relics,
    thought: effectPayload.selectableEffects,
  };

  assert.equal(nameCorrections.rules.length, 9);
  for (const rule of nameCorrections.rules) {
    const resolver = createNameCorrectionResolver({
      nameCorrections,
      kind: rule.kind,
      campaignId: rule.campaignId,
      catalog: catalogs[rule.kind],
    });
    for (const alias of rule.aliases) {
      const match = resolver.resolve(alias);
      assert.equal(match?.targetId, rule.targetId, `${rule.id}: ${alias}`);
      assert.equal(match?.matchType, "alias", `${rule.id}: ${alias}`);
    }
  }
});

test("every shared correction alias reaches its matching candidate extractor", async () => {
  const [nameCorrections, operatorPayload, relicPayload, effectPayload, operatorOcrMap] = await Promise.all([
    readJson("data/recognition/rhodes-name-corrections.json"),
    readJson("data/operators.json"),
    readJson("data/relics.json"),
    readJson("data/selectable-effects.json"),
    readJson("data/recognition/maa-operator-name-ocr.json"),
  ]);
  const extractors = {
    operator: createOperatorCandidateExtractor({ operators: operatorPayload.operators, operatorOcrMap, nameCorrections }),
    relic: createRelicCandidateExtractor({ relics: relicPayload.relics, campaignId: "is5_sarkaz", nameCorrections }),
    thought: createThoughtCandidateExtractor({ selectableEffects: effectPayload.selectableEffects, campaignId: "is5_sarkaz", nameCorrections }),
  };
  const contexts = {
    operator: { profile: { id: "operatorsFull" }, region: { x: 0, y: 0, width: 1200, height: 800 } },
    relic: { profile: { id: "relicsFull" }, campaignId: "is5_sarkaz", region: { x: 0, y: 0, width: 1200, height: 800 } },
    thought: { profile: { id: "is5ThoughtFull" }, campaignId: "is5_sarkaz", region: { x: 0, y: 0, width: 1200, height: 800 } },
  };

  for (const rule of nameCorrections.rules) {
    for (const alias of rule.aliases) {
      const candidates = await extractors[rule.kind]({
        ocrResults: [{ text: alias, confidence: 0.72, regionId: rule.kind === "operator" ? "operator.card.name.0" : "full", roi: { x: 100, y: 100, width: 180, height: 30 } }],
      }, contexts[rule.kind]);
      const candidate = candidates.find((item) => (item.operatorId || item.relicId || item.thoughtId) === rule.targetId);
      assert.ok(candidate, `${rule.id}: ${alias}`);
      assert.equal(candidate.rawText, alias, `${rule.id}: ${alias}`);
      assert.equal(candidate.source, "rhodes-name-correction", `${rule.id}: ${alias}`);

      const proseCandidates = await extractors[rule.kind]({
        ocrResults: [{ text: `${alias}を所持している場合`, confidence: 0.72, regionId: rule.kind === "operator" ? "operator.card.name.0" : "full", roi: { x: 100, y: 100, width: 260, height: 30 } }],
      }, contexts[rule.kind]);
      assert.equal(
        proseCandidates.some((item) => (item.operatorId || item.relicId || item.thoughtId) === rule.targetId),
        false,
        `${rule.id} prose: ${alias}`,
      );
    }
  }
});

test("name corrections stay inside their kind and campaign and reject ambiguous aliases", () => {
  const catalog = [
    { id: "one", name: "正式一", campaignId: "campaign-a" },
    { id: "two", name: "正式二", campaignId: "campaign-a" },
    { id: "other", name: "別正式", campaignId: "campaign-b" },
  ];
  const nameCorrections = {
    schemaVersion: 1,
    normalization: "nfkc-compact-quotes-lower",
    rules: [
      { id: "one", kind: "thought", campaignId: "campaign-a", targetId: "one", canonicalName: "正式一", aliases: ["共有誤読", "正式二"] },
      { id: "two", kind: "thought", campaignId: "campaign-a", targetId: "two", canonicalName: "正式二", aliases: ["共有誤読"] },
      { id: "other", kind: "thought", campaignId: "campaign-b", targetId: "other", canonicalName: "別正式", aliases: ["別誤読"] },
      { id: "wrong-kind", kind: "relic", campaignId: "campaign-a", targetId: "one", canonicalName: "正式一", aliases: ["種類違い"] },
      { id: "wrong-name", kind: "thought", campaignId: "campaign-a", targetId: "one", canonicalName: "不一致", aliases: ["無効"] },
    ],
  };
  const resolver = createNameCorrectionResolver({ nameCorrections, kind: "thought", campaignId: "campaign-a", catalog });

  assert.equal(resolver.resolve("共有誤読"), null);
  assert.equal(resolver.resolve("別誤読"), null);
  assert.equal(resolver.resolve("種類違い"), null);
  assert.equal(resolver.resolve("無効"), null);
  assert.deepEqual(resolver.resolve("正式二"), {
    targetId: "two",
    target: catalog[1],
    matchType: "formal",
    ruleId: null,
    normalizedText: "正式二",
  });
});

test("unsupported correction documents keep formal recognition but disable aliases", () => {
  const catalog = [{ id: "one", name: "正式一", campaignId: "campaign-a" }];
  const base = {
    schemaVersion: 1,
    normalization: "nfkc-compact-quotes-lower",
    rules: [
      { id: "one", kind: "thought", campaignId: "campaign-a", targetId: "one", canonicalName: "正式一", aliases: ["誤読"] },
    ],
  };

  for (const nameCorrections of [
    { ...base, schemaVersion: "1" },
    { ...base, normalization: "some-other-contract" },
  ]) {
    const resolver = createNameCorrectionResolver({ nameCorrections, kind: "thought", campaignId: "campaign-a", catalog });
    assert.equal(resolver.resolve("誤読"), null);
    assert.equal(resolver.resolve("正式一")?.matchType, "formal");
  }
});

test("aliases cannot rescue duplicate ids or ambiguous formal names", () => {
  const duplicateIds = [
    { id: "duplicate", name: "正式一", campaignId: "campaign-a" },
    { id: "duplicate", name: "正式二", campaignId: "campaign-a" },
  ];
  const ambiguousNames = [
    { id: "one", name: "同名", campaignId: "campaign-a" },
    { id: "two", name: "同名", campaignId: "campaign-a" },
    { id: "target", name: "正式三", campaignId: "campaign-a" },
  ];
  const document = (targetId, canonicalName, aliases) => ({
    schemaVersion: 1,
    normalization: "nfkc-compact-quotes-lower",
    rules: [{ id: "rule", kind: "thought", campaignId: "campaign-a", targetId, canonicalName, aliases }],
  });

  const duplicateResolver = createNameCorrectionResolver({
    nameCorrections: document("duplicate", "正式二", ["誤読"]),
    kind: "thought",
    campaignId: "campaign-a",
    catalog: duplicateIds,
  });
  assert.equal(duplicateResolver.resolve("誤読"), null);

  const ambiguousResolver = createNameCorrectionResolver({
    nameCorrections: document("target", "正式三", ["同名"]),
    kind: "thought",
    campaignId: "campaign-a",
    catalog: ambiguousNames,
  });
  assert.equal(ambiguousResolver.resolve("同名"), null);
});

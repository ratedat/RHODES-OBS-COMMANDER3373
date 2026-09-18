import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs/promises";
import { mergeImplementationHistory } from "../app/domain/operator-implementation-history.js";
import { sortOperators } from "../app/domain/operators.js";
import { createOperatorCandidateExtractor } from "../app/domain/recognition/operator-candidate-extractor.js";

const operatorOcrMap = JSON.parse(await fs.readFile(new URL("../data/recognition/maa-operator-name-ocr.json", import.meta.url), "utf8"));
const operatorCatalog = JSON.parse(await fs.readFile(new URL("../data/operators.json", import.meta.url), "utf8"));

test("MAA Japanese operator OCR map keeps raw CharsNameOcrReplace rules", () => {
  assert.equal(operatorOcrMap.source.project, "MaaAssistantArknights/MaaAssistantArknights");
  assert.equal(operatorOcrMap.source.taskId, "CharsNameOcrReplace");
  assert.equal(operatorOcrMap.replaceFull, true);
  assert.ok(operatorOcrMap.summary.rawRuleCount >= 1000);
  assert.ok(operatorOcrMap.rawOcrReplace.some(([pattern, replacement]) => pattern === "アーミヤ" && replacement === "阿米娅"));
});

test("MAA Japanese operator OCR map links representative rules to local operators", () => {
  const blaze = operatorOcrMap.rules.find((rule) => rule.pattern === "^ブレイ(ズ|ス)");
  assert.ok(blaze);
  assert.equal(blaze.maaReplacement, "煌");
  assert.deepEqual(blaze.localMatches.map((operator) => operator.id), ["blaze"]);

  const silverAsh = operatorOcrMap.rules.find((rule) => rule.maaReplacement === "银灰");
  assert.ok(silverAsh.localMatches.some((operator) => operator.id === "silverash"));

  const hoederer = operatorOcrMap.rules.find((rule) => rule.maaReplacement === "赫德雷");
  assert.ok(hoederer.localMatches.some((operator) => operator.id === "hoederer"));
});

test("MAA Japanese operator OCR map keeps Yu and Eunectes rules separate", () => {
  const yu = operatorOcrMap.rules.find((rule) => rule.pattern === "^ユー(?:$|[^ネ])");
  assert.ok(yu);
  assert.deepEqual(yu.localMatches.map((operator) => operator.id), ["yu"]);

  const eunectes = operatorOcrMap.rules.find((rule) => rule.pattern === "(ユ)?ーネクテス");
  assert.ok(eunectes);
  assert.deepEqual(eunectes.localMatches.map((operator) => operator.id), ["eunectes"]);
});

test("MAA Japanese operator OCR map includes OCR equivalence classes and recruitment names", () => {
  assert.ok(operatorOcrMap.equivalenceClasses.some((group) => group.includes("夕") && group.includes("タ")));
  assert.ok(operatorOcrMap.publicRecruitmentOperators.some((operator) => operator.name === "ジャスティスナイト"));
  assert.ok(operatorOcrMap.summary.publicRecruitmentOperatorCount >= 150);
});

test("operator catalog exposes current JP additions and keeps future operators hidden", () => {
  const operatorsById = new Map(operatorCatalog.operators.map((operator) => [operator.id, operator]));

  for (const id of ["bellone", "ripresa", "zima2", "ukusik", "botani"]) {
    const operator = operatorsById.get(id);
    assert.ok(operator, `${id} must exist in the operator catalog`);
    assert.equal(operator.isJapanUnreleased, false);
    assert.equal(operator.hiddenByDefault, false);
  }

  for (const id of ["mechanist", "thumpy", "angelina2", "jacinta", "timeslot", "makotoyuki", "aegis", "yukaritakeba", "koromaru"]) {
    const operator = operatorsById.get(id);
    assert.ok(operator, `${id} must exist in the operator catalog`);
    assert.equal(operator.isJapanUnreleased, true);
    assert.equal(operator.hiddenByDefault, true);
  }

  assert.equal(operatorOcrMap.summary.localOperatorCount, operatorCatalog.operators.length);
  assert.equal(operatorOcrMap.summary.invalidRegexRules, 0);
});

test("September JP additions have release dates and appear first in implementation order", async () => {
  const history = JSON.parse(await fs.readFile(new URL("../data/operator-implementation-history.json", import.meta.url), "utf8"));
  const { operators } = mergeImplementationHistory(operatorCatalog.operators, history.history);
  const ids = ["zima2", "ukusik", "botani"];
  for (const id of ids) {
    assert.equal(operators.find((operator) => operator.id === id)?.implementationDate, "2026-09-16", id);
  }
  const released = operators.filter((operator) => !operator.hiddenByDefault);
  assert.deepEqual(new Set(sortOperators(released, "implementation_desc").slice(0, 3).map((operator) => operator.id)), new Set(ids));
});

test("September JP names resolve uniquely without confusing Zima with her alter", async () => {
  const extractor = createOperatorCandidateExtractor({ operators: operatorCatalog.operators, operatorOcrMap });
  for (const [name, id] of [["怒潮ズィマー", "zima2"], ["怒潮ズイマー", "zima2"], ["ウクシク", "ukusik"], ["ボタニ", "botani"], ["ボ夕二", "botani"], ["ズィマー", "zima"]]) {
    const candidates = await extractor({
      ocrResults: [{ text: name, regionId: "operator.list_text", roi: { x: 20, y: 20, width: 260, height: 42 }, confidence: 0.99 }],
    }, { profile: { id: "operatorsFull" }, region: { x: 0, y: 0, width: 1280, height: 720 } });
    assert.deepEqual(candidates.map((candidate) => candidate.operatorId), [id], name);
  }
});

test("MAA Japanese operator OCR map recognizes current JP additions", () => {
  const bellone = operatorOcrMap.rules.find((rule) => rule.pattern === "ベッローネ");
  assert.ok(bellone);
  assert.equal(bellone.maaReplacement, "贝洛内");
  assert.deepEqual(bellone.localMatches.map((operator) => operator.id), ["bellone"]);

  const ripresa = operatorOcrMap.rules.find((rule) => rule.pattern === "リプレーザ");
  assert.ok(ripresa);
  assert.equal(ripresa.maaReplacement, "复奏");
  assert.deepEqual(ripresa.localMatches.map((operator) => operator.id), ["ripresa"]);
});

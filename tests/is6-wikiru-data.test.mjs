import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

const relics = JSON.parse(fs.readFileSync(new URL("../data/relics.json", import.meta.url), "utf8")).relics;
const squads = JSON.parse(fs.readFileSync(new URL("../data/squads.json", import.meta.url), "utf8")).squads;
const selectableEffects = JSON.parse(fs.readFileSync(new URL("../data/selectable-effects.json", import.meta.url), "utf8")).selectableEffects;
const difficultyGrades = JSON.parse(fs.readFileSync(new URL("../data/difficulty-grades.json", import.meta.url), "utf8"));

test("IS#6 July update includes every numbered relic through No.294", () => {
  const is6Relics = relics.filter((relic) => relic.campaignId === "is6_sui");
  const byNumber = new Map(is6Relics.map((relic) => [relic.number, relic]));

  assert.equal(is6Relics.length, 294);
  assert.deepEqual(
    Array.from({ length: 294 }, (_, index) => index + 1).filter((number) => !byNumber.has(number)),
    [],
  );
  assert.equal(byNumber.get(221)?.name, "不赦");
  assert.equal(byNumber.get(222)?.name, "不息");
  assert.equal(byNumber.get(222)?.image?.localPath, "assets/relics/wikiru/img/sggo_222_dlc2.png");
  assert.equal(byNumber.get(266)?.name, "驚堂木");
  assert.equal(byNumber.get(294)?.name, "花火の手");
});

test("IS#6 July update includes the three Content Addition II squads", () => {
  const is6Squads = squads.filter((squad) => squad.campaignId === "is6_sui");

  assert.equal(is6Squads.length, 19);
  assert.deepEqual(
    is6Squads.slice(-3).map((squad) => squad.name),
    ["代理人分隊", "知学分隊", "商人分隊"],
  );
});

test("IS#6 July update includes the Content Addition II coins and statuses", () => {
  const is6Effects = selectableEffects.filter((effect) => effect.campaignId === "is6_sui");
  const names = new Set(is6Effects.map((effect) => effect.name));

  assert.equal(is6Effects.length, 152);
  assert.equal(names.has("烽火台"), true);
  assert.equal(names.has("相合"), true);
  assert.equal(names.has("聖詔神を封ず"), true);
  assert.equal(names.has("触鎖の代幣"), true);
});

test("IS#6 current wiki data includes difficulty grades through 18", () => {
  const campaign = difficultyGrades.campaignDifficultyGrades.is6_sui;

  assert.equal(campaign.maxSelectableGrade, 18);
  assert.equal(campaign.grades.length, 18);
  assert.deepEqual(
    campaign.grades.slice(-3).map((grade) => [grade.grade, grade.enemyStrength]),
    [
      [16, "+18"],
      [17, "+21"],
      [18, "+25"],
    ],
  );
});

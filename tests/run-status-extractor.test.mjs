import test from "node:test";
import assert from "node:assert/strict";

import { extractRunStatusCandidates } from "../app/domain/recognition/run-status-extractor.js";

const squads = [
  { id: "is5_sarkaz_squad_02", campaignId: "is5_sarkaz", name: "博学多識分隊" },
  { id: "is5_sarkaz_squad_03", campaignId: "is5_sarkaz", name: "位置測定分隊" },
  { id: "is5_sarkaz_squad_04", campaignId: "is5_sarkaz", name: "指揮分隊" },
  {
    id: "is5_sarkaz_squad_16",
    campaignId: "is5_sarkaz",
    name: "奇想天外分隊",
    randomEffectOptions: [
      {
        id: "is5_sarkaz_mimic_02",
        label: "組み合わせ02",
        effect: "★4以上の【術師】を招集時に消費する希望-2、昇進時に消費する希望-1、【術師】を初めて招集する際、昇進済の状態で招集できる。初めから「生還者の契約」を所持",
      },
      {
        id: "is5_sarkaz_mimic_03",
        label: "組み合わせ03",
        effect: "★4以上の【特殊】を招集時に消費する希望-2、昇進時に消費する希望-1、【特殊】を初めて招集する際、昇進済の状態で招集できる。初めから「生還者の契約」を所持",
      },
    ],
  },
];

const difficultyGrades = {
  is5_sarkaz: {
    campaignId: "is5_sarkaz",
    difficultyName: "魂に直面",
    grades: Array.from({ length: 18 }, (_, index) => ({
      id: `is5_sarkaz_grade_${index + 1}`,
      campaignId: "is5_sarkaz",
      difficultyName: "魂に直面",
      grade: index + 1,
      label: `魂に直面・${index + 1}`,
    })),
  },
};

function fields(candidates) {
  return candidates.map((item) => [item.field, item.value]);
}

test("run status extractor keeps only retained base fields", () => {
  const candidates = extractRunStatusCandidates({
    ocrResults: [
      { text: "3 <8", regionId: "run.hope", confidence: 0.99 },
      { text: "3", regionId: "run.hope.current", confidence: 0.99 },
      { text: "8", regionId: "run.hope.max", confidence: 0.99 },
      { text: "20", regionId: "run.ingot", confidence: 0.99 },
      { text: "4/4", regionId: "run.life_points", confidence: 0.99 },
      { text: "2", regionId: "run.shield", confidence: 0.99 },
      { text: "1", regionId: "run.command_level", confidence: 0.99 },
      { text: "2", regionId: "run.idea.current.0", confidence: 0.99 },
      { text: "位 置 測 定 分 隊", regionId: "run.squad_card" },
      { text: "魂 に 直 面", regionId: "run.difficulty_block" },
      { text: "18", regionId: "run.difficulty_grade" },
    ],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.deepEqual(fields(candidates), [
    ["squadId", "is5_sarkaz_squad_03"],
    ["difficulty", 18],
    ["ingot", 20],
    ["idea", 2],
  ]);
  assert.equal(candidates.some((item) => ["hope", "maxHope", "lifePoints", "shield", "commandLevel"].includes(item.field)), false);
});

test("run status extractor maps OCR squad text and difficulty grade to current campaign IDs", () => {
  const candidates = extractRunStatusCandidates({
    ocrResults: [
      { text: "位 置 測 定 分 隊 ス ポ ッ ト 更 新 回 数 + 1", regionId: "run.squad_card" },
      { text: "魂 に 直 面 18 下 、 秘 宝", regionId: "run.difficulty_block" },
    ],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.deepEqual(fields(candidates), [
    ["squadId", "is5_sarkaz_squad_03"],
    ["difficulty", 18],
  ]);
});

test("run status extractor maps known Sarkaz squad OCR drift", () => {
  const candidates = extractRunStatusCandidates({
    ocrResults: [
      { text: "多 狙 分 隊", regionId: "run.squad_name" },
    ],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.deepEqual(fields(candidates), [
    ["squadId", "is5_sarkaz_squad_02"],
  ]);
});

test("run status extractor prefers dedicated difficulty grade ROI over decorative OCR noise", () => {
  const candidates = extractRunStatusCandidates({
    text: "魂 に 直 面 CDIFFICULTY\"I 5 位 置 測 定 分 隊",
    ocrResults: [{ text: "18", regionId: "run.difficulty_grade" }],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.equal(candidates.find((item) => item.field === "difficulty").value, 18);
});

test("run status extractor prefers labeled difficulty text over stray grade ROI digits", () => {
  const candidates = extractRunStatusCandidates({
    ocrResults: [
      { text: "魂 に 直 面 18", regionId: "run.difficulty_block" },
      { text: "2", regionId: "run.difficulty_grade" },
    ],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.equal(candidates.find((item) => item.field === "difficulty").value, 18);
});

test("run status extractor maps 奇想天外分隊 description to a random squad effect option", () => {
  const candidates = extractRunStatusCandidates({
    ocrResults: [
      { text: "奇 想 天 外 分 隊", regionId: "run.squad_name" },
      { text: "★4以上の【術師】を招集時に消費する希望-2、昇進時に消費する希望-1、【術師】を初めて招集する際、昇進済の状態で招集できる。初めから「生還者の契約」を所持", regionId: "run.squad_card" },
    ],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.deepEqual(candidates.filter((item) => ["squadId", "squadRandomEffectOptionId"].includes(item.field)).map((item) => [item.field, item.value]), [
    ["squadId", "is5_sarkaz_squad_16"],
    ["squadRandomEffectOptionId", "is5_sarkaz_mimic_02"],
  ]);
});

test("run status extractor does not infer random squad effect for non-奇想天外 squads", () => {
  const candidates = extractRunStatusCandidates({
    ocrResults: [
      { text: "指 揮 分 隊", regionId: "run.squad_name" },
      { text: "★4以上の【術師】を招集時に消費する希望-2、昇進時に消費する希望-1、【術師】を初めて招集する際、昇進済の状態で招集できる。初めから「生還者の契約」を所持", regionId: "run.squad_card" },
    ],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.equal(candidates.some((item) => item.field === "squadRandomEffectOptionId"), false);
});

test("run status extractor reads Sarkaz conception only from IS5", () => {
  const is5 = extractRunStatusCandidates({
    ocrResults: [{ text: "7", regionId: "run.idea.current.0", confidence: 0.99 }],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });
  const other = extractRunStatusCandidates({
    ocrResults: [{ text: "7", regionId: "run.idea.current.0", confidence: 0.99 }],
  }, { campaignId: "is4_sami", squads, difficultyGrades });

  assert.deepEqual(fields(is5), [["idea", 7]]);
  assert.equal(other.some((item) => item.field === "idea"), false);
});

test("run status extractor ignores thought burden fraction OCR as conception data", () => {
  const candidates = extractRunStatusCandidates({
    ocrResults: [
      { text: "0/5", regionId: "run.idea", confidence: 0.7 },
      { text: "思 考 負 荷", regionId: "run.idea", confidence: 0.7 },
      { text: "破 棘 成 金 分 隊", regionId: "run.squad_card" },
      { text: "魂 に 直 面", regionId: "run.difficulty_block" },
      { text: "18", regionId: "run.difficulty_grade" },
    ],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.equal(candidates.some((item) => item.field === "idea"), false);
});

test("run status extractor prefers template ingot and conception digits over noisy static OCR", () => {
  const candidates = extractRunStatusCandidates({
    ocrResults: [
      { regionId: "run.ingot.0", text: "02", confidence: 0.96 },
      { regionId: "run.ingot", text: "22", confidence: 0.70 },
      { regionId: "run.idea.current.0", text: "51", confidence: 0.96 },
    ],
  }, { campaignId: "is5_sarkaz", squads, difficultyGrades });

  assert.deepEqual(fields(candidates), [
    ["ingot", 20],
    ["idea", 1],
  ]);
});

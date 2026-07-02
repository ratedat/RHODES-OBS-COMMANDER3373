import test from "node:test";
import assert from "node:assert/strict";
import { maaTaskResultToRecognitionItems, maaTaskResultsToFrame, parseMaaRecognitionDetail } from "../app/domain/recognition/maa-resource-results.js";
import { extractRunStatusCandidates } from "../app/domain/recognition/run-status-extractor.js";

const pipeline = {
  RhodesOcrRegion_run_ingot: {
    attach: { id: "run.ingot" },
  },
  RhodesTemplate_runStatusFull_run_ingot: {
    attach: { idPrefix: "run.ingot" },
  },
};

test("parseMaaRecognitionDetail accepts raw JSON and C# summary strings", () => {
  assert.deepEqual(parseMaaRecognitionDetail('{"best":{"text":"6"}}'), { best: { text: "6" } });
  assert.deepEqual(parseMaaRecognitionDetail('TaskId=1; detail={"best":{"text":"20"}}'), { best: { text: "20" } });
});

test("maaTaskResultToRecognitionItems converts OCR detail into frame OCR rows", () => {
  const converted = maaTaskResultToRecognitionItems({
    entry: "RhodesOcrRegion_run_ingot",
    algorithm: "OCR",
    recognitionDetailJson: JSON.stringify({
      all: [{ text: "20", score: 0.91, box: [1190, 10, 90, 52] }],
      best: { text: "20", score: 0.91, box: [1190, 10, 90, 52] },
    }),
  }, { pipeline });

  assert.deepEqual(converted.ocrResults, [{
    text: "20",
    rawText: "20",
    regionId: "run.ingot",
    roi: { x: 1190, y: 10, width: 90, height: 52 },
    confidence: 0.91,
    source: "maa-framework",
    maaEntry: "RhodesOcrRegion_run_ingot",
  }]);
  assert.deepEqual(converted.templateResults, []);
});

test("maaTaskResultToRecognitionItems converts TemplateMatch detail into template rows", () => {
  const converted = maaTaskResultToRecognitionItems({
    entry: "RhodesTemplate_runStatusFull_run_ingot",
    algorithm: "TemplateMatch",
    recognitionDetailJson: JSON.stringify({
      all_results: [{ score: 0.88, box: [1048, 4, 70, 50] }],
      best_result: { score: 0.88, box: [1048, 4, 70, 50] },
    }),
  }, { pipeline });

  assert.equal(converted.ocrResults.length, 0);
  assert.deepEqual(converted.templateResults, [{
    regionId: "run.ingot",
    roi: { x: 1048, y: 4, width: 70, height: 50 },
    score: 0.88,
    source: "maa-framework",
    maaEntry: "RhodesTemplate_runStatusFull_run_ingot",
    count: null,
  }]);
});

test("maaTaskResultsToFrame feeds existing run status candidate extraction", () => {
  const frame = maaTaskResultsToFrame([
    {
      entry: "RhodesOcrRegion_run_ingot",
      algorithm: "OCR",
      recognitionDetailJson: JSON.stringify({ best: { text: "20", score: 0.96, box: [1190, 10, 90, 52] } }),
    },
  ], { pipeline });
  const candidates = extractRunStatusCandidates(frame, {
    squads: [],
    campaignId: "is5_sarkaz",
    difficultyGrades: {},
  });

  assert.equal(candidates.find((candidate) => candidate.field === "ingot")?.value, 20);
  assert.equal(candidates.some((candidate) => ["hope", "maxHope"].includes(candidate.field)), false);
});

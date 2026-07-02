import test from "node:test";
import assert from "node:assert/strict";

import { runMaaResourceRecognition } from "../app/domain/recognition/maa-resource-scan-runner.js";
import { extractRunStatusCandidates } from "../app/domain/recognition/run-status-extractor.js";

const pipeline = {
  RhodesOcrRegion_run_ingot: {
    attach: { id: "run.ingot" },
  },
};

test("runMaaResourceRecognition feeds MAA Resource OCR results into existing candidate extractors", async () => {
  const result = await runMaaResourceRecognition({
    profile: { id: "runStatusFull", label: "基礎情報" },
    pipeline,
    taskResults: [
      {
        entry: "RhodesOcrRegion_run_ingot",
        algorithm: "OCR",
        recognitionDetailJson: JSON.stringify({ best: { text: "20", score: 0.96, box: [1190, 10, 90, 52] } }),
      },
    ],
    candidateExtractors: [extractRunStatusCandidates],
    recognitionContext: {
      campaignId: "is5_sarkaz",
      squads: [],
      difficultyGrades: {},
    },
    scanId: "maa-resource-scan-1",
    now: () => new Date("2026-06-30T10:00:00.000Z"),
  });

  assert.equal(result.status, "completed");
  assert.deepEqual(result.counts, {
    taskResults: 1,
    ocrResults: 1,
    templateResults: 0,
    candidates: 1,
    suggestions: 1,
  });
  assert.equal(result.frame.source, "maa-framework");
  assert.equal(result.candidates.find((candidate) => candidate.field === "ingot")?.value, 20);
  assert.deepEqual(result.suggestions.map((suggestion) => suggestion.profileId), ["runStatusFull"]);
});

test("runMaaResourceRecognition drops abandoned run status candidates from extractor output", async () => {
  const result = await runMaaResourceRecognition({
    profile: { id: "runStatusFull", label: "基礎情報" },
    pipeline,
    taskResults: [
      {
        entry: "RhodesOcrRegion_run_ingot",
        algorithm: "OCR",
        recognitionDetailJson: JSON.stringify({ best: { text: "20", score: 0.96 } }),
      },
    ],
    candidateExtractors: [
      async () => [
        { kind: "runStatus", field: "hope", label: "希望", value: 3, rawText: "3", confidence: 0.99 },
        { kind: "runStatus", field: "ingot", label: "源石錐", value: 20, rawText: "20", confidence: 0.96 },
      ],
    ],
    recognitionContext: { campaignId: "is5_sarkaz" },
    scanId: "maa-resource-scan-filter",
    now: () => new Date("2026-06-30T10:00:00.000Z"),
  });

  assert.deepEqual(result.candidates.map((candidate) => candidate.field), ["ingot"]);
  assert.deepEqual(result.suggestions.map((suggestion) => suggestion.candidate.field), ["ingot"]);
  assert.equal(result.counts.candidates, 1);
  assert.equal(result.counts.suggestions, 1);
});

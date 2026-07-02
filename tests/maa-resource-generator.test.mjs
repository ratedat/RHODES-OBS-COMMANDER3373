import test from "node:test";
import assert from "node:assert/strict";

import { generatePipeline } from "../tools/generate-maa-resource.mjs";
import {
  isAbandonedRunField,
  isAbandonedRunMaaEntry,
  isRetainedRecognitionSource,
  maaRecognitionIdTokens,
} from "../tools/maa-recognition-policy.mjs";

const ocrRecognition = {
  type: "OCR",
  roi: [1, 2, 3, 4],
};

const templateRegion = (idPrefix) => ({
  idPrefix,
  templatePath: "assets/recognition/templates/run/IngotIcon.png",
  searchRoi: { x: 1, y: 2, width: 3, height: 4 },
});

test("MAA recognition policy defines the retained run target boundary once", () => {
  assert.deepEqual(maaRecognitionIdTokens("RhodesOcrRegion_run_command_level"), [
    "rhodes",
    "ocr",
    "region",
    "run",
    "command",
    "level",
  ]);
  assert.equal(isAbandonedRunField("hope"), true);
  assert.equal(isRetainedRecognitionSource({ id: "run.hope.current" }), false);
  assert.equal(isRetainedRecognitionSource({ id: "run.ingot" }), true);
  assert.equal(isRetainedRecognitionSource({ id: "run.idea.current" }), true);
  assert.equal(isRetainedRecognitionSource({ id: "run.safe" }), false);
  assert.equal(isRetainedRecognitionSource({ id: "run.safe", candidateField: "commandLevel" }), false);
  assert.equal(isAbandonedRunMaaEntry("RhodesOcrRegion_run_shield"), true);
  assert.equal(isAbandonedRunMaaEntry("RhodesOcrRegion_run_safe"), true);
  assert.equal(isAbandonedRunMaaEntry("RhodesOcrRegion_run_ingot"), false);
  assert.equal(isAbandonedRunMaaEntry("RhodesRunStatusIdeaIcon"), false);
  assert.equal(isAbandonedRunMaaEntry("RhodesTemplate_runStatusFull_run_ingot"), false);
});

test("MAA resource generator refuses abandoned run value targets even if source JSON contains them", () => {
  const pipeline = generatePipeline({
    maaTasks: {
      screens: [
        { id: "run.hope.panel", recognition: ocrRecognition },
      ],
      candidates: [
        {
          id: "run.hope.candidate",
          recognition: ocrRecognition,
          candidate: { kind: "runStatus", field: "hope" },
        },
      ],
      ocrRegions: [
        { id: "run.hope.current", roi: [1, 2, 3, 4] },
        { id: "run.life.points", roi: [1, 2, 3, 4] },
        { id: "run.shield", roi: [1, 2, 3, 4] },
        { id: "run.command.level", roi: [1, 2, 3, 4] },
        { id: "run.safe", roi: [1, 2, 3, 4] },
        { id: "run.ingot", roi: [5, 6, 7, 8] },
      ],
    },
    scanProfiles: {
      profiles: [
        {
          id: "runStatusFull",
          templateOcrRegions: [
            templateRegion("run.top.hope"),
            templateRegion("run.ingot"),
          ],
        },
      ],
    },
  });

  assert.equal(pipeline.RhodesScreen_run_hope_panel, undefined);
  assert.equal(pipeline.RhodesCandidate_run_hope_candidate, undefined);
  assert.equal(pipeline.RhodesOcrRegion_run_hope_current, undefined);
  assert.equal(pipeline.RhodesOcrRegion_run_life_points, undefined);
  assert.equal(pipeline.RhodesOcrRegion_run_shield, undefined);
  assert.equal(pipeline.RhodesOcrRegion_run_command_level, undefined);
  assert.equal(pipeline.RhodesOcrRegion_run_safe, undefined);
  assert.equal(pipeline.RhodesTemplate_runStatusFull_run_top_hope, undefined);
  assert.equal(pipeline.RhodesOcrRegion_run_ingot.recognition, "OCR");
  assert.equal(pipeline.RhodesTemplate_runStatusFull_run_ingot.recognition, "TemplateMatch");
});

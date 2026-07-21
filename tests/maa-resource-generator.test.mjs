import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";

import { generatePipeline } from "../tools/generate-maa-resource.mjs";
import {
  abandonedRunFieldIds,
  isAbandonedRunField,
  isAbandonedRunMaaEntry,
  isRetainedRecognitionSource,
  isRetainedRecognitionCandidate,
  maaRecognitionIdTokens,
  retainedCandidateKinds,
  retainedRunRecognitionIds,
  retainedRunStatusFields,
  targetPolicyPath,
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
  const manifest = JSON.parse(fs.readFileSync(targetPolicyPath, "utf8"));
  assert.equal(manifest.schemaVersion, 1);
  assert.deepEqual(retainedCandidateKinds, manifest.recognitionTargets.retainedCandidateKinds);
  assert.deepEqual(retainedRunStatusFields, manifest.runRecognition.retainedFields);
  assert.deepEqual(retainedRunRecognitionIds, manifest.runRecognition.retainedIds);
  assert.deepEqual(abandonedRunFieldIds, manifest.runRecognition.abandonedFields);
  assert.deepEqual(retainedCandidateKinds, ["runStatus", "operator", "relic", "thought", "age", "mizuki", "revelation", "coin"]);
  assert.deepEqual(retainedRunStatusFields, [
    "ingot",
    "difficulty",
    "squadId",
    "squadRandomEffectOptionId",
    "idea",
    "ticket",
    "performanceId",
    "hallucinations",
  ]);
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
  assert.equal(isRetainedRecognitionCandidate({ kind: "operator", operatorId: "char_002_amiya" }), true);
  assert.equal(isRetainedRecognitionCandidate({ kind: "relic", relicId: "is5_sarkaz_relic_001" }), true);
  assert.equal(isRetainedRecognitionCandidate({ kind: "thought", thoughtId: "thought_001" }), true);
  assert.equal(isRetainedRecognitionCandidate({ kind: "runStatus", field: "ingot" }), true);
  assert.equal(isRetainedRecognitionCandidate({ kind: "runStatus", field: "hope" }), false);
  assert.equal(isRetainedRecognitionCandidate({ kind: "status", field: "lifePoints" }), false);
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

test("MAA resource generator batches ordered squad templates into one Or recognition", () => {
  const pipeline = generatePipeline({
    maaTasks: {},
    scanProfiles: {
      profiles: [
        {
          id: "runStatusFull",
          templateOcrRegions: [
            {
              idPrefix: "run.squad.icon.is5_sarkaz.batch",
              templatePaths: [
                "assets/recognition/templates/run/SquadIconRight_is5_sarkaz_squad_01.png",
                "assets/recognition/templates/run/SquadIconRight_is5_sarkaz_squad_02.png",
              ],
              templateIds: ["is5_sarkaz_squad_01", "is5_sarkaz_squad_02"],
              searchRoi: { x: 58, y: 632, width: 50, height: 88 },
              threshold: 0.72,
              method: 5,
            },
          ],
        },
      ],
    },
  });

  const node = pipeline.RhodesTemplate_runStatusFull_run_squad_icon_is5_sarkaz_batch;
  assert.equal(node.recognition, "Or");
  assert.equal(node.any_of.length, 2);
  assert.equal(node.any_of[1].recognition, "TemplateMatch");
  assert.equal(node.any_of[1].template, "run/SquadIconRight_is5_sarkaz_squad_02.png");
  assert.deepEqual(node.attach.templateIds, ["is5_sarkaz_squad_01", "is5_sarkaz_squad_02"]);
});

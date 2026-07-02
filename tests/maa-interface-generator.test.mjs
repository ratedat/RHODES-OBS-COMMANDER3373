import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import { generateInterface } from "../tools/generate-maa-interface.mjs";

function readJson(path) {
  return JSON.parse(fs.readFileSync(path, "utf8"));
}

test("MAA interface generator exposes every retained RHODES resource node as a task", () => {
  const manualPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes.json");
  const generatedPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes-generated.json");
  const projectInterface = generateInterface({ manualPipeline, generatedPipeline });
  const taskEntries = new Set(projectInterface.task.map((task) => task.entry));

  assert.equal(projectInterface.interface_version, 2);
  assert.equal(projectInterface.controller[0].name, "android_adb");
  assert.equal(projectInterface.resource[0].name, "base");
  assert.ok(taskEntries.has("RhodesOperatorNameOcr"));
  assert.ok(taskEntries.has("RhodesTemplate_operatorsFull_operator_card_name"));
  assert.ok(taskEntries.has("RhodesOcrRegion_run_ingot"));
  assert.ok(taskEntries.has("RhodesOcrRegion_is5_thought_list_text"));
  assert.equal(taskEntries.has("RhodesGeneratedEmpty"), false);
  assert.equal(taskEntries.has("RhodesEmpty"), false);

  const expectedEntries = [
    ...Object.keys(manualPipeline).filter((entry) => !/Empty$/.test(entry)),
    ...Object.keys(generatedPipeline).filter((entry) => !/Empty$/.test(entry)),
  ];
  for (const entry of expectedEntries) {
    assert.ok(taskEntries.has(entry), `${entry} should be published in interface.json`);
  }
});

test("MAA interface generator never publishes abandoned run value nodes", () => {
  const projectInterface = generateInterface({
    manualPipeline: {
      RhodesRunStatusHopeIcon: { recognition: "TemplateMatch" },
      RhodesRunStatusLifePoints: { recognition: "OCR" },
      RhodesRunStatusShield: { recognition: "OCR" },
      RhodesRunStatusCommandLevel: { recognition: "OCR" },
      RhodesRunStatusIdeaIcon: { recognition: "TemplateMatch" },
    },
    generatedPipeline: {
      RhodesOcrRegion_run_hope_current: { recognition: "OCR" },
      RhodesOcrRegion_run_life_points: { recognition: "OCR" },
      RhodesOcrRegion_run_shield: { recognition: "OCR" },
      RhodesOcrRegion_run_command_level: { recognition: "OCR" },
      RhodesOcrRegion_run_ingot: { recognition: "OCR" },
    },
  });
  const taskEntries = new Set(projectInterface.task.map((task) => task.entry));

  assert.equal(taskEntries.has("RhodesRunStatusHopeIcon"), false);
  assert.equal(taskEntries.has("RhodesRunStatusLifePoints"), false);
  assert.equal(taskEntries.has("RhodesRunStatusShield"), false);
  assert.equal(taskEntries.has("RhodesRunStatusCommandLevel"), false);
  assert.equal(taskEntries.has("RhodesOcrRegion_run_hope_current"), false);
  assert.equal(taskEntries.has("RhodesOcrRegion_run_life_points"), false);
  assert.equal(taskEntries.has("RhodesOcrRegion_run_shield"), false);
  assert.equal(taskEntries.has("RhodesOcrRegion_run_command_level"), false);
  assert.equal(taskEntries.has("RhodesRunStatusIdeaIcon"), true);
  assert.equal(taskEntries.has("RhodesOcrRegion_run_ingot"), true);
});

test("checked-in MAA interface stays synchronized with generated task metadata", () => {
  const manualPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes.json");
  const generatedPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes-generated.json");
  const expected = `${JSON.stringify(generateInterface({ manualPipeline, generatedPipeline }), null, 2)}\n`;
  const actual = fs.readFileSync("apps/rhodes-suki/interface.json", "utf8");

  assert.equal(actual, expected);
});

test("package scripts check MAA resource and interface before Suki publish", () => {
  const packageJson = readJson("package.json");

  assert.equal(packageJson.scripts["maa:interface:generate"], "node tools/generate-maa-interface.mjs");
  assert.equal(packageJson.scripts["maa:interface:check"], "node tools/generate-maa-interface.mjs --check");
  assert.match(packageJson.scripts["suki:publish:portable"], /npm run maa:resource:generate && npm run maa:interface:generate/);
});

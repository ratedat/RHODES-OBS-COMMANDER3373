import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { validateInterfaceContract } from "../tools/check-maa-contract.mjs";
import { generateInterface, generateInterfaceBundle } from "../tools/generate-maa-interface.mjs";

function readJson(path) {
  return JSON.parse(fs.readFileSync(path, "utf8"));
}

test("MAA interface generator exposes every retained RHODES resource node as a task", () => {
  const manualPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes.json");
  const generatedPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes-generated.json");
  const bundle = generateInterfaceBundle({ manualPipeline, generatedPipeline, resourceHash: "test-hash" });
  const projectInterface = bundle.projectInterface;
  const taskEntries = new Set(projectInterface.task.map((task) => task.entry));

  assert.equal(projectInterface.interface_version, 2);
  assert.match(bundle.translations.ja_jp[projectInterface.description.slice(1)], /1280x720/);
  assert.match(bundle.translations.ja_jp[projectInterface.description.slice(1)], /maa-recognition-target-policy\.json/);
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

test("MAA interface generator groups tasks and presets by RHODES recognition profile", () => {
  const manualPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes.json");
  const generatedPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes-generated.json");
  const scanProfiles = readJson("data/recognition/scan-profiles.json");
  const bundle = generateInterfaceBundle({ manualPipeline, generatedPipeline, resourceHash: "test-hash" });
  const projectInterface = bundle.projectInterface;
  const ja = (value) => bundle.translations.ja_jp[value.slice(1)];
  const groups = new Map(projectInterface.group.map((group) => [group.name, group]));
  const presets = new Map(projectInterface.preset.map((preset) => [preset.name, preset]));
  const tasks = new Map(projectInterface.task.map((task) => [task.entry, task]));
  const runStatusSource = scanProfiles.profiles.find((profile) => profile.id === "runStatusFull");

  assert.deepEqual([...groups.keys()], [
    "runStatusFull",
    "operatorsFull",
    "relicsFull",
    "is4RevelationFull",
    "is4ParadigmLost",
    "is5ThoughtFull",
    "is5AgeFull",
    "is2HallucinationsFull",
    "is2PerformanceFull",
    "is3KeyFull",
    "is3LightHordeFull",
    "is3RejectionFull",
    "is6BaseFull",
    "is6ActiveCoinsFull",
    "is6SeasonalHours",
    "is6CoinsFull",
  ]);
  assert.equal(ja(groups.get("runStatusFull").label), "基礎情報");
  assert.equal(ja(groups.get("runStatusFull").label), runStatusSource.interfaceLabel);
  assert.match(ja(groups.get("runStatusFull").description), /源石錐/);
  assert.doesNotMatch(runStatusSource.interfaceDescription, /希望|耐久値|シールド|指揮Lv/);
  assert.doesNotMatch(ja(groups.get("runStatusFull").description), /希望|耐久値|シールド|指揮Lv/);
  assert.match(ja(groups.get("runStatusFull").description), /maa-recognition-target-policy\.json/);
  assert.equal(ja(groups.get("operatorsFull").label), "オペレーター");
  assert.equal(groups.get("is5ThoughtFull").default_expand, false);

  assert.deepEqual(tasks.get("RhodesRunStatusIngotIcon").group, ["runStatusFull"]);
  assert.deepEqual(tasks.get("RhodesOcrRegion_run_ingot").group, ["runStatusFull"]);
  assert.deepEqual(tasks.get("RhodesOperatorNameOcr").group, ["operatorsFull"]);
  assert.deepEqual(tasks.get("RhodesScreen_run_map_footer").group, [
    "runStatusFull",
    "operatorsFull",
    "relicsFull",
    "is4RevelationFull",
    "is4ParadigmLost",
    "is5ThoughtFull",
    "is5AgeFull",
    "is2HallucinationsFull",
    "is2PerformanceFull",
    "is3KeyFull",
    "is3LightHordeFull",
    "is3RejectionFull",
    "is6BaseFull",
    "is6ActiveCoinsFull",
    "is6SeasonalHours",
    "is6CoinsFull",
  ]);
  assert.equal(Object.hasOwn(tasks.get("RhodesProbe"), "group"), false);

  assert.deepEqual([...presets.keys()], [...groups.keys()]);
  assert.equal(ja(presets.get("runStatusFull").label), "基礎情報");
  assert.match(ja(presets.get("operatorsFull").description), /オペレーター/);
  const runStatusPresetTasks = new Set(presets.get("runStatusFull").task.map((task) => task.name));
  const operatorPresetTasks = new Set(presets.get("operatorsFull").task.map((task) => task.name));
  assert.equal(runStatusPresetTasks.has("rhodes_ocr_region_run_ingot"), true);
  assert.equal(runStatusPresetTasks.has("rhodes_operator_name_ocr"), false);
  assert.equal(operatorPresetTasks.has("rhodes_operator_name_ocr"), true);
  assert.equal(operatorPresetTasks.has("rhodes_ocr_region_run_ingot"), false);
});

test("MAA interface generator emits complete PI V2 translations and a resource hash", () => {
  const manualPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes.json");
  const generatedPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes-generated.json");
  const bundle = generateInterfaceBundle({
    manualPipeline,
    generatedPipeline,
    resourceHash: "resource-hash-for-test",
  });
  const projectInterface = bundle.projectInterface;

  assert.deepEqual(projectInterface.languages, {
    ja_jp: "interface_ja_jp.json",
    en_us: "interface_en_us.json",
  });
  assert.equal(projectInterface.resource[0].hash, "resource-hash-for-test");
  assert.equal(projectInterface.telemetry, undefined);
  assert.equal(projectInterface.label, "$project.label");
  assert.equal(projectInterface.group[0].label, "$group.runStatusFull.label");

  const referencedKeys = [];
  const visit = (value) => {
    if (Array.isArray(value)) {
      value.forEach(visit);
      return;
    }
    if (value && typeof value === "object") {
      Object.values(value).forEach(visit);
      return;
    }
    if (typeof value === "string" && value.startsWith("$")) referencedKeys.push(value.slice(1));
  };
  visit(projectInterface);

  for (const language of ["ja_jp", "en_us"]) {
    for (const key of referencedKeys) {
      assert.equal(typeof bundle.translations[language][key], "string", `${language} contains ${key}`);
      assert.notEqual(bundle.translations[language][key].trim(), "", `${language} translates ${key}`);
    }
  }
  assert.equal(bundle.translations.ja_jp["group.runStatusFull.label"], "基礎情報");
  assert.equal(bundle.translations.en_us["group.runStatusFull.label"], "Run status");
});

test("MAA interface contract validates translation files and referenced keys", () => {
  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), "rhodes-pi-i18n-"));
  try {
    fs.mkdirSync(path.join(tempRoot, "resource", "base"), { recursive: true });
    fs.writeFileSync(path.join(tempRoot, "interface_ja_jp.json"), JSON.stringify({ "project.label": "RHODES" }));
    fs.writeFileSync(path.join(tempRoot, "interface_en_us.json"), JSON.stringify({ "project.label": "RHODES" }));
    const projectInterface = {
      interface_version: 2,
      languages: {
        ja_jp: "interface_ja_jp.json",
        en_us: "interface_en_us.json",
      },
      name: "rhodes",
      label: "$project.label",
      controller: [{ name: "android_adb" }],
      resource: [{ name: "base", path: ["resource/base"], controller: ["android_adb"], hash: "abc123" }],
      group: [],
      task: [],
      preset: [],
    };

    assert.deepEqual(validateInterfaceContract(projectInterface, { appDirectory: tempRoot }).errors, []);
    fs.writeFileSync(path.join(tempRoot, "interface_en_us.json"), "{}");
    assert.match(
      validateInterfaceContract(projectInterface, { appDirectory: tempRoot }).errors.join("\n"),
      /interface_en_us\.json.*project\.label/,
    );
  } finally {
    fs.rmSync(tempRoot, { recursive: true, force: true });
  }
});

test("checked-in MAA interface stays synchronized with generated task metadata", () => {
  const manualPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes.json");
  const generatedPipeline = readJson("apps/rhodes-suki/resource/base/pipeline/rhodes-generated.json");
  const expected = `${JSON.stringify(generateInterface({ manualPipeline, generatedPipeline }), null, 2)}\n`;
  const actual = fs.readFileSync("apps/rhodes-suki/interface.json", "utf8");

  assert.equal(actual, expected);
});

test("MAA interface contract catches broken task, group, resource, and preset references", () => {
  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), "rhodes-pi-contract-"));
  fs.mkdirSync(path.join(tempRoot, "resource", "base"), { recursive: true });
  fs.writeFileSync(path.join(tempRoot, "interface_ja_jp.json"), "{}");
  fs.writeFileSync(path.join(tempRoot, "interface_en_us.json"), "{}");
  const valid = {
    interface_version: 2,
    languages: {
      ja_jp: "interface_ja_jp.json",
      en_us: "interface_en_us.json",
    },
    controller: [{ name: "android_adb" }],
    resource: [{ name: "base", path: ["resource/base"], controller: ["android_adb"], hash: "abc123" }],
    group: [{ name: "operatorsFull", label: "オペレーター" }],
    task: [
      {
        name: "rhodes_operator_name_ocr",
        entry: "RhodesOperatorNameOcr",
        controller: ["android_adb"],
        resource: ["base"],
        group: ["operatorsFull"],
      },
    ],
    preset: [
      {
        name: "operatorsFull",
        task: [{ name: "rhodes_operator_name_ocr", option: { enabled: true } }],
      },
    ],
  };
  const pipelineEntries = new Set(["RhodesOperatorNameOcr"]);

  assert.deepEqual(validateInterfaceContract(valid, { pipelineEntries, appDirectory: tempRoot }).errors, []);

  const broken = structuredClone(valid);
  broken.task[0].entry = "MissingEntry";
  broken.task[0].controller = ["missing_controller"];
  broken.task[0].resource = ["missing_resource"];
  broken.task[0].group = ["missingGroup"];
  broken.preset[0].task = [{ name: "missing_task", option: { enabled: true } }];

  const errors = validateInterfaceContract(broken, { pipelineEntries, appDirectory: tempRoot }).errors.join("\n");
  assert.match(errors, /unknown pipeline entry MissingEntry/);
  assert.match(errors, /unknown controller missing_controller/);
  assert.match(errors, /unknown resource missing_resource/);
  assert.match(errors, /unknown group missingGroup/);
  assert.match(errors, /unknown task missing_task/);

  const duplicated = structuredClone(valid);
  duplicated.task.push({ ...duplicated.task[0] });
  assert.match(validateInterfaceContract(duplicated, { pipelineEntries, appDirectory: tempRoot }).errors.join("\n"), /duplicate task name/);

  const abandoned = structuredClone(valid);
  abandoned.task[0].entry = "RhodesOcrRegion_run_hope_current";
  const abandonedErrors = validateInterfaceContract(abandoned, {
    pipelineEntries: new Set(["RhodesOcrRegion_run_hope_current"]),
    appDirectory: tempRoot,
  }).errors.join("\n");
  assert.match(abandonedErrors, /pipeline contains abandoned run target RhodesOcrRegion_run_hope_current/);
  assert.match(abandonedErrors, /publishes private or abandoned pipeline entry RhodesOcrRegion_run_hope_current/);
  fs.rmSync(tempRoot, { recursive: true, force: true });
});

test("package scripts check MAA resource and interface before Suki publish", () => {
  const packageJson = readJson("package.json");
  const portablePublisher = fs.readFileSync("tools/publish-suki-portable.mjs", "utf8");

  assert.equal(packageJson.scripts["maa:interface:generate"], "node tools/generate-maa-interface.mjs");
  assert.equal(packageJson.scripts["maa:interface:check"], "node tools/generate-maa-interface.mjs --check");
  assert.equal(packageJson.scripts["maa:resource:hash:generate"], "node tools/update-maa-resource-hash.mjs");
  assert.equal(packageJson.scripts["maa:resource:hash:check"], "node tools/update-maa-resource-hash.mjs --check");
  assert.equal(packageJson.scripts["maa:contract:check"], "node tools/check-maa-contract.mjs");
  assert.equal(packageJson.scripts["maa:check"], "npm run maa:resource:check && npm run maa:resource:hash:check && npm run maa:interface:check && npm run maa:contract:check");
  assert.equal(packageJson.scripts["suki:check"], "npm run maa:check && npm run suki:test");
  assert.equal(packageJson.scripts["suki:publish:portable"], "node tools/publish-suki-portable.mjs");
  assert.match(portablePublisher, /generate-maa-resource\.mjs/);
  assert.match(portablePublisher, /generate-maa-interface\.mjs/);
  assert.match(portablePublisher, /update-maa-resource-hash\.mjs/);
  assert.match(portablePublisher, /check-maa-contract\.mjs/);
  assert.match(portablePublisher, /PublishSingleFile=true/);
  assert.match(portablePublisher, /removeFilesByExtension\(outputDir, "\.pdb"\)/);
  assert.match(portablePublisher, /libs", "MaaAgentBinary"/);
  assert.match(portablePublisher, /copyMaaNativeRuntimeToRuntimes/);
  assert.match(portablePublisher, /"runtimes", "win-x64", "native"/);
  assert.match(portablePublisher, /MaaAdbControlUnit\.dll/);
  assert.match(portablePublisher, /opencv_world4_maa\.dll/);
});

import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { isPublishableMaaEntry, targetPolicyPath } from "./maa-recognition-policy.mjs";

const root = process.cwd();
const manualPipelinePath = path.join(root, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes.json");
const generatedPipelinePath = path.join(root, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes-generated.json");
const scanProfilesPath = path.join(root, "data", "recognition", "scan-profiles.json");
const outputPath = path.join(root, "apps", "rhodes-suki", "interface.json");
const japaneseOutputPath = path.join(root, "apps", "rhodes-suki", "interface_ja_jp.json");
const englishOutputPath = path.join(root, "apps", "rhodes-suki", "interface_en_us.json");
const resourceHashMetadataPath = path.join(root, "apps", "rhodes-suki", "interface.resource-hash.json");

const CONTROLLER = "android_adb";
const RESOURCE = "base";
const TARGET_POLICY_SOURCE = path.relative(root, targetPolicyPath).replace(/\\/g, "/");

const PROFILE_METADATA = profileMetadataFromScanProfiles(readJson(scanProfilesPath));
const PROFILE_ORDER = new Map(PROFILE_METADATA.map((profile, index) => [profile.id, index]));

const MANUAL_LABELS = new Map([
  ["RhodesProbe", ["Probe", "MAAFramework接続確認用のDirectHitタスクです。"]],
  ["RhodesRunStatusIdeaIcon", ["基本情報: 構想アイコン", "構想値の基準点になるアイコンTemplateMatchです。", ["runStatusFull"]]],
  ["RhodesRunStatusIngotIcon", ["基本情報: 源石錐アイコン", "源石錐の基準点になるアイコンTemplateMatchです。", ["runStatusFull"]]],
  ["RhodesOperatorCodenameFlag", ["オペレーター: CODENAME", "招集カード内のCODENAME目印をMAA TemplateMatchで検出します。", ["operatorsFull"]]],
  ["RhodesOperatorNameOcr", ["オペレーター: 名前OCR", "招集カード領域をMAA-OCRで読ませます。", ["operatorsFull"]]],
  ["RhodesRelicButton", ["画面判定: 秘宝ボタン", "マップ下部の秘宝ボタンをMAA TemplateMatchで検出します。", ["relicsFull"]]],
  ["RhodesOperatorButton", ["画面判定: 隊員ボタン", "マップ下部の隊員ボタンをMAA TemplateMatchで検出します。", ["operatorsFull"]]],
  ["RhodesThoughtButton", ["画面判定: 思案ボタン", "マップ下部の思案ボタンをMAA TemplateMatchで検出します。", ["is5ThoughtFull"]]],
]);

const PROFILE_ENGLISH_LABELS = new Map([
  ["runStatusFull", "Run status"],
  ["operatorsFull", "Operators"],
  ["relicsFull", "Relics"],
  ["is4RevelationFull", "Revelations"],
  ["is4ParadigmLost", "Paradigm Lost"],
  ["is5ThoughtFull", "Thoughts"],
  ["is5AgeFull", "Age"],
  ["is2HallucinationsFull", "Hallucinations"],
  ["is2PerformanceFull", "Performances"],
  ["is3KeyFull", "Ingots and keys"],
  ["is3LightHordeFull", "Light and hordes"],
  ["is3RejectionFull", "Rejections and revelations"],
  ["is6BaseFull", "Sui base values"],
  ["is6ActiveCoinsFull", "Active Sui coins"],
  ["is6SeasonalHours", "Seasonal hours"],
  ["is6CoinsFull", "Owned Sui coins"],
]);

const MANUAL_ENGLISH_LABELS = new Map([
  ["RhodesProbe", "Probe"],
  ["RhodesRunStatusIdeaIcon", "Run status: conception icon"],
  ["RhodesRunStatusIngotIcon", "Run status: ingot icon"],
  ["RhodesOperatorCodenameFlag", "Operator: CODENAME"],
  ["RhodesOperatorNameOcr", "Operator: name OCR"],
  ["RhodesRelicButton", "Screen check: relic button"],
  ["RhodesOperatorButton", "Screen check: operator button"],
  ["RhodesThoughtButton", "Screen check: thought button"],
]);

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function readResourceHash() {
  if (!fs.existsSync(resourceHashMetadataPath)) return "";
  const metadata = readJson(resourceHashMetadataPath);
  return typeof metadata?.hash === "string" ? metadata.hash.trim() : "";
}

function taskName(entry) {
  return String(entry)
    .replace(/([a-z0-9])([A-Z])/g, "$1_$2")
    .replace(/[^A-Za-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .toLowerCase();
}

function stringArray(value) {
  if (typeof value === "string" && value.trim()) return [value.trim()];
  if (!Array.isArray(value)) return [];
  return [...new Set(value.filter((item) => typeof item === "string").map((item) => item.trim()).filter(Boolean))];
}

function orderedProfileIds(profileIds) {
  return [...new Set(profileIds.filter((id) => PROFILE_ORDER.has(id)))]
    .sort((left, right) => PROFILE_ORDER.get(left) - PROFILE_ORDER.get(right));
}

function profileMetadataFromScanProfiles(scanProfiles) {
  return (scanProfiles?.profiles ?? [])
    .map((profile) => {
      const id = String(profile?.id ?? "").trim();
      const label = String(profile?.interfaceLabel ?? profile?.label ?? id).trim();
      const baseDescription = String(profile?.interfaceDescription ?? profile?.navigationNote ?? "").trim();
      const description = id === "runStatusFull" && !baseDescription.includes(TARGET_POLICY_SOURCE)
        ? `${baseDescription} target policy: ${TARGET_POLICY_SOURCE}`
        : baseDescription;
      return { id, label: label || id, description: description || "RHODES認識プロファイルです。" };
    })
    .filter((profile) => profile.id);
}

function withGroups(task, profileIds) {
  const groups = orderedProfileIds(profileIds);
  if (!groups.length) return task;
  return { ...task, group: groups };
}

function taskProfileIds(task) {
  return Array.isArray(task.group) ? task.group : [];
}

function generatedTask(entry, node) {
  const attach = node?.attach ?? {};
  const recognition = typeof node?.recognition === "string" ? node.recognition : "";
  const label = attach.label || attach.id || entry;
  const sourceParts = [recognition, attach.source, attach.id].filter(Boolean);
  const profileIds = stringArray(attach.profileIds);
  if (attach.profileId && !profileIds.includes(attach.profileId)) profileIds.push(attach.profileId);
  if (profileIds.length) sourceParts.push(`profiles: ${profileIds.join(", ")}`);
  return withGroups({
    name: taskName(entry),
    label: `生成: ${label}`,
    entry,
    controller: [CONTROLLER],
    resource: [RESOURCE],
    description: sourceParts.length ? sourceParts.join(" / ") : "生成済みMAA Resourceノードです。",
  }, profileIds);
}

function manualTask(entry, node) {
  const [label, description, profileIds = []] = MANUAL_LABELS.get(entry) ?? [entry, "RHODES手動定義のMAA Resourceタスクです。", []];
  const recognition = typeof node?.recognition === "string" ? node.recognition : "";
  return withGroups({
    name: taskName(entry),
    label,
    entry,
    controller: [CONTROLLER],
    resource: [RESOURCE],
    description: [description, recognition].filter(Boolean).join(" / "),
  }, profileIds);
}

function buildTasks(manualPipeline, generatedPipeline) {
  const tasks = [];
  const seen = new Set();
  for (const [entry, node] of Object.entries(manualPipeline ?? {})) {
    if (!isPublishableMaaEntry(entry) || seen.has(entry)) continue;
    seen.add(entry);
    tasks.push(manualTask(entry, node));
  }
  for (const [entry, node] of Object.entries(generatedPipeline ?? {})) {
    if (!isPublishableMaaEntry(entry) || seen.has(entry)) continue;
    seen.add(entry);
    tasks.push(generatedTask(entry, node));
  }
  return tasks;
}

function buildGroups(tasks) {
  const usedProfileIds = new Set(tasks.flatMap(taskProfileIds));
  return PROFILE_METADATA
    .filter((profile) => usedProfileIds.has(profile.id))
    .map((profile, index) => ({
      name: profile.id,
      label: profile.label,
      description: profile.description,
      default_expand: index < 3,
    }));
}

function buildPresets(groups, tasks) {
  return groups.map((group) => ({
    name: group.name,
    label: group.label,
    description: `${group.label}プロファイルのResource taskだけを選択します。`,
    task: tasks
      .filter((task) => taskProfileIds(task).includes(group.name))
      .map((task) => ({ name: task.name, option: { enabled: true } })),
  }));
}

function rawInterface({ manualPipeline, generatedPipeline, resourceHash = "" }) {
  const tasks = buildTasks(manualPipeline, generatedPipeline);
  const groups = buildGroups(tasks);
  return {
    interface_version: 2,
    languages: {
      ja_jp: "interface_ja_jp.json",
      en_us: "interface_en_us.json",
    },
    name: "rhodes_obs_commander3373",
    label: "RHODES OBS COMMANDER3373",
    title: "RHODES OBS COMMANDER3373",
    version: "0.1.0",
    github: "https://github.com/ratedat/RHODES-OBS-COMMANDER3373",
    license: "AGPL-3.0-only",
    description: `MAAFramework resource shell for Arknights Integrated Strategies OCR and OBS support. Fixed coordinate base: 1280x720. Target policy: ${TARGET_POLICY_SOURCE}.`,
    controller: [
      {
        name: CONTROLLER,
        label: "Android / Google Play Games / Emulator ADB",
        type: "Adb",
        display_short_side: 720,
        adb: {},
      },
    ],
    resource: [
      {
        name: RESOURCE,
        label: "RHODES Base 1280x720",
        path: ["resource/base"],
        controller: [CONTROLLER],
        ...(resourceHash ? { hash: resourceHash } : {}),
      },
    ],
    group: groups,
    task: tasks,
    preset: buildPresets(groups, tasks),
  };
}

function localizeInterface(projectInterface) {
  const localized = structuredClone(projectInterface);
  const jaJp = {};
  const enUs = {};
  const translate = (target, field, key, english) => {
    const japanese = target?.[field];
    if (typeof japanese !== "string" || !japanese.trim()) return;
    jaJp[key] = japanese;
    enUs[key] = typeof english === "string" && english.trim() ? english : japanese;
    target[field] = `$${key}`;
  };

  translate(localized, "label", "project.label", "RHODES OBS COMMANDER3373");
  translate(localized, "title", "project.title", "RHODES OBS COMMANDER3373");
  translate(
    localized,
    "description",
    "project.description",
    projectInterface.description,
  );

  for (const controller of localized.controller ?? []) {
    translate(
      controller,
      "label",
      `controller.${controller.name}.label`,
      "Android / Google Play Games / Emulator ADB",
    );
    translate(
      controller,
      "description",
      `controller.${controller.name}.description`,
      "MAAFramework ADB controller for capture and recognition.",
    );
  }

  for (const resource of localized.resource ?? []) {
    translate(resource, "label", `resource.${resource.name}.label`, "RHODES Base 1280x720");
    translate(
      resource,
      "description",
      `resource.${resource.name}.description`,
      "RHODES recognition resource using a 1280x720 coordinate base.",
    );
  }

  for (const group of localized.group ?? []) {
    const englishLabel = PROFILE_ENGLISH_LABELS.get(group.name) ?? group.name;
    translate(group, "label", `group.${group.name}.label`, englishLabel);
    translate(
      group,
      "description",
      `group.${group.name}.description`,
      `${englishLabel} recognition profile.`,
    );
  }

  for (const task of localized.task ?? []) {
    const key = `task.${task.name}`;
    const englishLabel = MANUAL_ENGLISH_LABELS.get(task.entry) ?? `Generated: ${task.entry}`;
    translate(task, "label", `${key}.label`, englishLabel);
    translate(
      task,
      "description",
      `${key}.description`,
      `Recognition-only MAA resource node: ${task.entry}.`,
    );
  }

  for (const preset of localized.preset ?? []) {
    const englishLabel = PROFILE_ENGLISH_LABELS.get(preset.name) ?? preset.name;
    translate(preset, "label", `preset.${preset.name}.label`, englishLabel);
    translate(
      preset,
      "description",
      `preset.${preset.name}.description`,
      `Select only the resource tasks in the ${englishLabel} profile.`,
    );
  }

  return {
    projectInterface: localized,
    translations: { ja_jp: jaJp, en_us: enUs },
  };
}

export function generateInterfaceBundle({ manualPipeline, generatedPipeline, resourceHash = readResourceHash() }) {
  return localizeInterface(rawInterface({ manualPipeline, generatedPipeline, resourceHash }));
}

export function generateInterface(options) {
  return generateInterfaceBundle(options).projectInterface;
}

function serializedInterface(projectInterface) {
  return `${JSON.stringify(projectInterface, null, 2)}\n`;
}

function generate() {
  return generateInterfaceBundle({
    manualPipeline: readJson(manualPipelinePath),
    generatedPipeline: readJson(generatedPipelinePath),
  });
}

function writeGenerated({ projectInterface, translations }) {
  fs.writeFileSync(outputPath, serializedInterface(projectInterface));
  fs.writeFileSync(japaneseOutputPath, serializedInterface(translations.ja_jp));
  fs.writeFileSync(englishOutputPath, serializedInterface(translations.en_us));
  console.log(`Generated ${projectInterface.task.length} MAA interface tasks: ${outputPath}`);
}

function checkGenerated({ projectInterface, translations }) {
  const expectedFiles = new Map([
    [outputPath, serializedInterface(projectInterface)],
    [japaneseOutputPath, serializedInterface(translations.ja_jp)],
    [englishOutputPath, serializedInterface(translations.en_us)],
  ]);
  const stale = [...expectedFiles].filter(([filePath, expected]) => (
    !fs.existsSync(filePath) || fs.readFileSync(filePath, "utf8") !== expected
  ));
  if (!stale.length) {
    console.log(`MAA interface is up to date: ${outputPath}`);
    return;
  }

  for (const [filePath] of stale) console.error(`stale: ${filePath}`);
  console.error("MAA interface is stale. Run npm run maa:interface:generate");
  process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const projectInterface = generate();
  if (process.argv.includes("--check")) {
    checkGenerated(projectInterface);
  } else {
    writeGenerated(projectInterface);
  }
}

import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { isPublishableMaaEntry, targetPolicyPath } from "./maa-recognition-policy.mjs";

const root = process.cwd();
const manualPipelinePath = path.join(root, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes.json");
const generatedPipelinePath = path.join(root, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes-generated.json");
const scanProfilesPath = path.join(root, "data", "recognition", "scan-profiles.json");
const outputPath = path.join(root, "apps", "rhodes-suki", "interface.json");

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

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
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

export function generateInterface({ manualPipeline, generatedPipeline }) {
  const tasks = buildTasks(manualPipeline, generatedPipeline);
  const groups = buildGroups(tasks);
  return {
    interface_version: 2,
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
      },
    ],
    group: groups,
    task: tasks,
    preset: buildPresets(groups, tasks),
  };
}

function serializedInterface(projectInterface) {
  return `${JSON.stringify(projectInterface, null, 2)}\n`;
}

function generate() {
  return generateInterface({
    manualPipeline: readJson(manualPipelinePath),
    generatedPipeline: readJson(generatedPipelinePath),
  });
}

function writeGenerated(projectInterface) {
  fs.writeFileSync(outputPath, serializedInterface(projectInterface));
  console.log(`Generated ${projectInterface.task.length} MAA interface tasks: ${outputPath}`);
}

function checkGenerated(projectInterface) {
  const expected = serializedInterface(projectInterface);
  const actual = fs.existsSync(outputPath) ? fs.readFileSync(outputPath, "utf8") : "";
  if (actual === expected) {
    console.log(`MAA interface is up to date: ${outputPath}`);
    return;
  }

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

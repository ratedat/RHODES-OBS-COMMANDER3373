import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";

const root = process.cwd();
const manualPipelinePath = path.join(root, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes.json");
const generatedPipelinePath = path.join(root, "apps", "rhodes-suki", "resource", "base", "pipeline", "rhodes-generated.json");
const outputPath = path.join(root, "apps", "rhodes-suki", "interface.json");

const CONTROLLER = "android_adb";
const RESOURCE = "base";
const ABANDONED_RUN_ID_TOKENS = new Set(["hope", "maxhope", "life", "lifepoints", "shield", "command", "commandlevel"]);

const MANUAL_LABELS = new Map([
  ["RhodesProbe", ["Probe", "MAAFramework接続確認用のDirectHitタスクです。"]],
  ["RhodesRunStatusIdeaIcon", ["基本情報: 構想アイコン", "構想値の基準点になるアイコンTemplateMatchです。"]],
  ["RhodesRunStatusIngotIcon", ["基本情報: 源石錐アイコン", "源石錐の基準点になるアイコンTemplateMatchです。"]],
  ["RhodesOperatorCodenameFlag", ["オペレーター: CODENAME", "招集カード内のCODENAME目印をMAA TemplateMatchで検出します。"]],
  ["RhodesOperatorNameOcr", ["オペレーター: 名前OCR", "招集カード領域をMAA-OCRで読ませます。"]],
  ["RhodesRelicButton", ["画面判定: 秘宝ボタン", "マップ下部の秘宝ボタンをMAA TemplateMatchで検出します。"]],
  ["RhodesOperatorButton", ["画面判定: 隊員ボタン", "マップ下部の隊員ボタンをMAA TemplateMatchで検出します。"]],
  ["RhodesThoughtButton", ["画面判定: 思案ボタン", "マップ下部の思案ボタンをMAA TemplateMatchで検出します。"]],
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

function isPublishableEntry(entry) {
  if (!entry || /Empty$/.test(entry)) return false;
  const tokens = String(entry)
    .replace(/([a-z])([A-Z])/g, "$1_$2")
    .toLowerCase()
    .split(/[^a-z0-9]+/g)
    .filter(Boolean);
  return !tokens.some((token) => ABANDONED_RUN_ID_TOKENS.has(token));
}

function stringArray(value) {
  if (typeof value === "string" && value.trim()) return [value.trim()];
  if (!Array.isArray(value)) return [];
  return [...new Set(value.filter((item) => typeof item === "string").map((item) => item.trim()).filter(Boolean))];
}

function generatedTask(entry, node) {
  const attach = node?.attach ?? {};
  const recognition = typeof node?.recognition === "string" ? node.recognition : "";
  const label = attach.label || attach.id || entry;
  const sourceParts = [recognition, attach.source, attach.id].filter(Boolean);
  const profileIds = stringArray(attach.profileIds);
  if (attach.profileId && !profileIds.includes(attach.profileId)) profileIds.push(attach.profileId);
  if (profileIds.length) sourceParts.push(`profiles: ${profileIds.join(", ")}`);
  return {
    name: taskName(entry),
    label: `生成: ${label}`,
    entry,
    controller: [CONTROLLER],
    resource: [RESOURCE],
    description: sourceParts.length ? sourceParts.join(" / ") : "生成済みMAA Resourceノードです。",
  };
}

function manualTask(entry, node) {
  const [label, description] = MANUAL_LABELS.get(entry) ?? [entry, "RHODES手動定義のMAA Resourceタスクです。"];
  const recognition = typeof node?.recognition === "string" ? node.recognition : "";
  return {
    name: taskName(entry),
    label,
    entry,
    controller: [CONTROLLER],
    resource: [RESOURCE],
    description: [description, recognition].filter(Boolean).join(" / "),
  };
}

function buildTasks(manualPipeline, generatedPipeline) {
  const tasks = [];
  const seen = new Set();
  for (const [entry, node] of Object.entries(manualPipeline ?? {})) {
    if (!isPublishableEntry(entry) || seen.has(entry)) continue;
    seen.add(entry);
    tasks.push(manualTask(entry, node));
  }
  for (const [entry, node] of Object.entries(generatedPipeline ?? {})) {
    if (!isPublishableEntry(entry) || seen.has(entry)) continue;
    seen.add(entry);
    tasks.push(generatedTask(entry, node));
  }
  return tasks;
}

export function generateInterface({ manualPipeline, generatedPipeline }) {
  return {
    interface_version: 2,
    name: "rhodes_obs_commander3373",
    label: "RHODES OBS COMMANDER3373",
    title: "RHODES OBS COMMANDER3373",
    version: "0.1.0",
    github: "https://github.com/ratedat/RHODES-OBS-COMMANDER3373",
    license: "AGPL-3.0-only",
    description: "MAAFramework resource shell for Arknights Integrated Strategies OCR and OBS support.",
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
    task: buildTasks(manualPipeline, generatedPipeline),
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

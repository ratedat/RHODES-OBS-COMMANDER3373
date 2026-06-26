import fs from "node:fs/promises";
import path from "node:path";

const root = process.cwd();
const thirdPartyRoot = path.join(root, "third_party", "maa", "resource", "tasks");
const outputFile = path.join(root, "data", "recognition", "maa-ocr-rules.json");

async function readJson(file) {
  return JSON.parse(await fs.readFile(file, "utf8"));
}

function collectTaskReplacements(sourceName, tasks) {
  const items = [];
  for (const [taskId, task] of Object.entries(tasks || {})) {
    if (!Array.isArray(task?.ocrReplace) || !task.ocrReplace.length) continue;
    items.push({
      source: sourceName,
      taskId,
      baseTask: task.baseTask || null,
      ocrReplace: task.ocrReplace,
      replaceFull: Boolean(task.replaceFull),
      withoutDet: Boolean(task.withoutDet),
      isAscii: Boolean(task.isAscii),
      roi: Array.isArray(task.roi) ? task.roi : null,
    });
  }
  return items;
}

const commonTasks = await readJson(path.join(thirdPartyRoot, "tasks.json"));
const roguelikeFiles = [
  path.join(thirdPartyRoot, "Roguelike", "base.json"),
  path.join(thirdPartyRoot, "Roguelike", "Sarkaz.json"),
  path.join(thirdPartyRoot, "Roguelike", "Sami.json"),
  path.join(thirdPartyRoot, "Roguelike", "JieGarden.json"),
];
const roguelikeTasks = [];
for (const file of roguelikeFiles) {
  roguelikeTasks.push(...collectTaskReplacements(path.relative(root, file).replaceAll("\\", "/"), await readJson(file)));
}

const payload = {
  version: 1,
  source: {
    project: "MaaAssistantArknights/MaaAssistantArknights",
    branch: "dev-v2",
    license: "AGPL-3.0-only",
    localPath: "third_party/maa",
  },
  numberOcrReplace: commonTasks.NumberOcrReplace?.ocrReplace || [],
  commonOcrReplaceTasks: collectTaskReplacements("third_party/maa/resource/tasks/tasks.json", commonTasks),
  roguelikeOcrReplaceTasks: roguelikeTasks,
};

await fs.mkdir(path.dirname(outputFile), { recursive: true });
await fs.writeFile(outputFile, `${JSON.stringify(payload, null, 2)}\n`, "utf8");
console.log(`wrote ${path.relative(root, outputFile)}`);

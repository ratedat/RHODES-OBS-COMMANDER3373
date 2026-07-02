import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { isAbandonedRunMaaEntry, isPublishableMaaEntry } from "./maa-recognition-policy.mjs";

const root = process.cwd();
const appRoot = path.join(root, "apps", "rhodes-suki");
const interfacePath = path.join(appRoot, "interface.json");
const manualPipelinePath = path.join(appRoot, "resource", "base", "pipeline", "rhodes.json");
const generatedPipelinePath = path.join(appRoot, "resource", "base", "pipeline", "rhodes-generated.json");

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function arrayOfStrings(value) {
  return Array.isArray(value) ? value.filter((item) => typeof item === "string" && item.trim()).map((item) => item.trim()) : [];
}

function addDuplicateErrors(errors, values, label) {
  const seen = new Set();
  for (const value of values) {
    if (!value) continue;
    if (seen.has(value)) errors.push(`duplicate ${label}: ${value}`);
    seen.add(value);
  }
}

function setEquals(left, right) {
  if (left.size !== right.size) return false;
  for (const item of left) {
    if (!right.has(item)) return false;
  }
  return true;
}

export function validateInterfaceContract(projectInterface, { pipelineEntries = new Set(), appDirectory = appRoot } = {}) {
  const errors = [];
  const controllers = new Set((projectInterface.controller ?? []).map((item) => item?.name).filter(Boolean));
  const resources = new Set((projectInterface.resource ?? []).map((item) => item?.name).filter(Boolean));
  const groups = new Set((projectInterface.group ?? []).map((item) => item?.name).filter(Boolean));
  const tasks = Array.isArray(projectInterface.task) ? projectInterface.task : [];
  const presets = Array.isArray(projectInterface.preset) ? projectInterface.preset : [];
  const taskNames = new Set(tasks.map((task) => task?.name).filter(Boolean));

  for (const entry of pipelineEntries) {
    if (isAbandonedRunMaaEntry(entry)) errors.push(`pipeline contains abandoned run target ${entry}`);
  }

  addDuplicateErrors(errors, (projectInterface.controller ?? []).map((item) => item?.name), "controller");
  addDuplicateErrors(errors, (projectInterface.resource ?? []).map((item) => item?.name), "resource");
  addDuplicateErrors(errors, (projectInterface.group ?? []).map((item) => item?.name), "group");
  addDuplicateErrors(errors, tasks.map((task) => task?.name), "task name");
  addDuplicateErrors(errors, tasks.map((task) => task?.entry), "task entry");
  addDuplicateErrors(errors, presets.map((preset) => preset?.name), "preset");

  for (const resource of projectInterface.resource ?? []) {
    for (const controllerName of arrayOfStrings(resource?.controller)) {
      if (!controllers.has(controllerName)) errors.push(`resource ${resource.name} references unknown controller ${controllerName}`);
    }
    for (const resourcePath of arrayOfStrings(resource?.path)) {
      const absolutePath = path.resolve(appDirectory, resourcePath);
      if (!fs.existsSync(absolutePath)) errors.push(`resource ${resource.name} path does not exist: ${resourcePath}`);
    }
  }

  for (const task of tasks) {
    if (!task?.name) errors.push("task is missing name");
    if (!task?.entry) errors.push(`task ${task?.name ?? "<unknown>"} is missing entry`);
    if (task?.entry && !isPublishableMaaEntry(task.entry)) {
      errors.push(`task ${task.name ?? "<unknown>"} publishes private or abandoned pipeline entry ${task.entry}`);
    }
    if (task?.entry && pipelineEntries.size > 0 && !pipelineEntries.has(task.entry)) {
      errors.push(`task ${task.name} references unknown pipeline entry ${task.entry}`);
    }

    for (const controllerName of arrayOfStrings(task?.controller)) {
      if (!controllers.has(controllerName)) errors.push(`task ${task.name} references unknown controller ${controllerName}`);
    }
    for (const resourceName of arrayOfStrings(task?.resource)) {
      if (!resources.has(resourceName)) errors.push(`task ${task.name} references unknown resource ${resourceName}`);
    }
    for (const groupName of arrayOfStrings(task?.group)) {
      if (!groups.has(groupName)) errors.push(`task ${task.name} references unknown group ${groupName}`);
    }
  }

  for (const preset of presets) {
    const presetName = preset?.name ?? "";
    if (!groups.has(presetName)) errors.push(`preset ${presetName || "<unknown>"} has no matching group`);
    const presetTaskNames = new Set((preset?.task ?? []).map((item) => item?.name).filter(Boolean));
    const groupedTaskNames = new Set(tasks
      .filter((task) => arrayOfStrings(task?.group).includes(presetName))
      .map((task) => task.name)
      .filter(Boolean));

    for (const taskName of presetTaskNames) {
      if (!taskNames.has(taskName)) errors.push(`preset ${presetName} references unknown task ${taskName}`);
    }
    if (groups.has(presetName) && !setEquals(presetTaskNames, groupedTaskNames)) {
      const missing = [...groupedTaskNames].filter((taskName) => !presetTaskNames.has(taskName));
      const extra = [...presetTaskNames].filter((taskName) => !groupedTaskNames.has(taskName));
      if (missing.length) errors.push(`preset ${presetName} is missing grouped tasks: ${missing.join(", ")}`);
      if (extra.length) errors.push(`preset ${presetName} contains non-group tasks: ${extra.join(", ")}`);
    }
  }

  return { ok: errors.length === 0, errors };
}

function loadPipelineEntries() {
  return new Set([
    ...Object.keys(readJson(manualPipelinePath)),
    ...Object.keys(readJson(generatedPipelinePath)),
  ]);
}

function check() {
  const projectInterface = readJson(interfacePath);
  const result = validateInterfaceContract(projectInterface, {
    pipelineEntries: loadPipelineEntries(),
    appDirectory: appRoot,
  });
  if (result.ok) {
    console.log(`MAA interface contract is valid: ${interfacePath}`);
    return;
  }

  console.error("MAA interface contract errors:");
  for (const error of result.errors) console.error(`- ${error}`);
  process.exitCode = 1;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  check();
}

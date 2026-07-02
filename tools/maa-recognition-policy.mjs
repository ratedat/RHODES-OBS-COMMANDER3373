import fs from "node:fs";
import { fileURLToPath } from "node:url";

export const targetPolicyPath = fileURLToPath(new URL("../data/recognition/maa-recognition-target-policy.json", import.meta.url));

const targetPolicy = JSON.parse(fs.readFileSync(targetPolicyPath, "utf8"));
const runRecognitionPolicy = targetPolicy.runRecognition ?? {};

export const abandonedRunFieldIds = Object.freeze(stringArray(runRecognitionPolicy.abandonedFields));
export const retainedRunRecognitionIds = Object.freeze(stringArray(runRecognitionPolicy.retainedIds));

const abandonedRunFieldSet = new Set(abandonedRunFieldIds);
const retainedRunRecognitionIdSet = new Set(retainedRunRecognitionIds);

export function maaRecognitionIdTokens(value) {
  return String(value || "")
    .replace(/([a-z])([A-Z])/g, "$1.$2")
    .toLowerCase()
    .split(/[^a-z0-9]+/g)
    .filter(Boolean);
}

export function isAbandonedRunRecognitionId(id) {
  const retainedId = retainedRunRecognitionId(id);
  return retainedId !== "" && !retainedRunRecognitionIdSet.has(retainedId);
}

export function isAbandonedRunField(fieldId) {
  return abandonedRunFieldSet.has(fieldId);
}

export function isRetainedRecognitionSource({ id, candidateField } = {}) {
  return !isAbandonedRunRecognitionId(id) && !isAbandonedRunField(candidateField);
}

export function isAbandonedRunMaaEntry(entry) {
  return isAbandonedRunRecognitionId(entry);
}

export function isPublishableMaaEntry(entry) {
  if (!entry || /Empty$/.test(entry)) return false;
  return !isAbandonedRunMaaEntry(entry);
}

function retainedRunRecognitionId(id) {
  const tokens = maaRecognitionIdTokens(id);
  const runIndex = tokens.lastIndexOf("run");
  return runIndex < 0 ? "" : tokens.slice(runIndex).join(".");
}

function stringArray(value) {
  return Array.isArray(value) ? value.filter((item) => typeof item === "string" && item.trim()).map((item) => item.trim()) : [];
}

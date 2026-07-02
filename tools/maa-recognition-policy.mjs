export const abandonedRunFieldIds = Object.freeze(["hope", "maxHope", "lifePoints", "shield", "commandLevel"]);
export const retainedRunRecognitionIds = Object.freeze([
  "run.squad.info.panel",
  "run.status.idea.icon",
  "run.status.ingot.icon",
  "run.operator.list",
  "run.sarkaz.age.detail",
  "run.map.footer",
  "run.map.footer.relic",
  "run.ingot",
  "run.idea",
  "run.idea.current",
  "run.squad.card",
  "run.squad.name",
  "run.difficulty.grade",
  "run.difficulty.block",
]);

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

export const abandonedRunFieldIds = Object.freeze(["hope", "maxHope", "lifePoints", "shield", "commandLevel"]);

const abandonedRunFieldSet = new Set(abandonedRunFieldIds);
const abandonedRunIdTokens = new Set(["hope", "maxhope", "life", "lifepoints", "shield", "command", "commandlevel"]);

export function maaRecognitionIdTokens(value) {
  return String(value || "")
    .replace(/([a-z])([A-Z])/g, "$1.$2")
    .toLowerCase()
    .split(/[^a-z0-9]+/g)
    .filter(Boolean);
}

export function isAbandonedRunRecognitionId(id) {
  const tokens = maaRecognitionIdTokens(id);
  return tokens[0] === "run" && tokens.slice(1).some((token) => abandonedRunIdTokens.has(token));
}

export function isAbandonedRunField(fieldId) {
  return abandonedRunFieldSet.has(fieldId);
}

export function isRetainedRecognitionSource({ id, candidateField } = {}) {
  return !isAbandonedRunRecognitionId(id) && !isAbandonedRunField(candidateField);
}

export function isAbandonedRunMaaEntry(entry) {
  return maaRecognitionIdTokens(entry).some((token) => abandonedRunIdTokens.has(token));
}

export function isPublishableMaaEntry(entry) {
  if (!entry || /Empty$/.test(entry)) return false;
  return !isAbandonedRunMaaEntry(entry);
}

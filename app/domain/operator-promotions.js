export function normalizeOperatorPromotionLevels(value, operatorIds = []) {
  const selected = new Set(Array.isArray(operatorIds) ? operatorIds.filter(Boolean) : []);
  if (!value || typeof value !== "object" || Array.isArray(value)) return {};

  const normalized = {};
  for (const [operatorId, rawLevel] of Object.entries(value)) {
    if (!selected.has(operatorId)) continue;
    if (Number(rawLevel) >= 2) normalized[operatorId] = 2;
  }
  return normalized;
}

export function operatorPromotionLevelFor(operatorId, promotionLevels = {}) {
  return Number(promotionLevels?.[operatorId]) >= 2 ? 2 : 1;
}

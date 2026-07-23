export const MAX_OPERATOR_COUNT = 99;

export function isReserveOperatorId(id) {
  return typeof id === "string" && /^reserve_/i.test(id.trim());
}

export function normalizeOperatorCounts(value, operatorIds = []) {
  const selected = new Set(Array.isArray(operatorIds) ? operatorIds : []);
  const result = {};
  if (!value || typeof value !== "object" || Array.isArray(value)) return result;

  for (const [id, rawCount] of Object.entries(value)) {
    if (!selected.has(id) || !isReserveOperatorId(id)) continue;
    const numeric = Number(rawCount);
    if (!Number.isFinite(numeric)) continue;
    const count = Math.min(MAX_OPERATOR_COUNT, Math.max(1, Math.trunc(numeric)));
    if (count > 1) result[id] = count;
  }
  return result;
}

export function operatorCountFor(id, counts = {}) {
  if (!isReserveOperatorId(id)) return 1;
  const normalized = Number(counts?.[id]);
  if (!Number.isFinite(normalized)) return 1;
  return Math.min(MAX_OPERATOR_COUNT, Math.max(1, Math.trunc(normalized)));
}

export function withOperatorCounts(operators, counts = {}) {
  return (Array.isArray(operators) ? operators : []).map((operator) => ({
    ...operator,
    count: operatorCountFor(operator?.id, counts),
  }));
}

export function operatorRosterCount(operators) {
  return (Array.isArray(operators) ? operators : []).reduce((sum, operator) => {
    const count = Number(operator?.count);
    return sum + (Number.isFinite(count) ? Math.max(1, Math.trunc(count)) : 1);
  }, 0);
}

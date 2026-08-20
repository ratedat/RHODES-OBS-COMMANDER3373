function ruleList(rules) {
  return Array.isArray(rules) ? rules : (Array.isArray(rules?.rules) ? rules.rules : []);
}

function nonStackRelicList(rules) {
  return Array.isArray(rules?.nonStackRelics) ? rules.nonStackRelics : [];
}

function positiveInteger(value) {
  const number = Number(value);
  return Number.isSafeInteger(number) && number > 0 ? number : 0;
}

export function relicStackRuleFor(relicId, rules) {
  return ruleList(rules).find((rule) => rule?.relicId === relicId) || null;
}

export function relicIsExplicitlyNonStack(relicId, rules) {
  return nonStackRelicList(rules).some((relic) => relic?.relicId === relicId);
}

export function relicStackMaximum(relicId, rules) {
  const rule = relicStackRuleFor(relicId, rules);
  if (!rule) return undefined;
  const maximum = positiveInteger(rule.maximum);
  return maximum || null;
}

export function relicStackCountFor(relicId, counts) {
  return positiveInteger(counts?.[relicId]);
}

export function relicSupportsStackCount(relicId, counts, rules) {
  if (relicIsExplicitlyNonStack(relicId, rules)) return false;
  return Boolean(relicStackRuleFor(relicId, rules)) || relicStackCountFor(relicId, counts) > 0;
}

export function normalizeRelicStackCounts(value, ownedRelicIds, rules) {
  const owned = new Set(Array.isArray(ownedRelicIds) ? ownedRelicIds : []);
  const source = value && typeof value === "object" && !Array.isArray(value) ? value : {};
  const normalized = {};
  for (const [relicId, rawCount] of Object.entries(source)) {
    const count = positiveInteger(rawCount);
    if (!owned.has(relicId) || count <= 0 || relicIsExplicitlyNonStack(relicId, rules)) continue;
    const maximum = relicStackMaximum(relicId, rules);
    if (typeof maximum === "number" && count > maximum) continue;
    normalized[relicId] = count;
  }
  return normalized;
}

export function normalizeManualRelicStackCount(value, relicId, rules) {
  if (relicIsExplicitlyNonStack(relicId, rules)) return 0;
  const count = positiveInteger(value);
  if (count <= 0) return 0;
  const maximum = relicStackMaximum(relicId, rules);
  return typeof maximum === "number" ? Math.min(count, maximum) : count;
}
